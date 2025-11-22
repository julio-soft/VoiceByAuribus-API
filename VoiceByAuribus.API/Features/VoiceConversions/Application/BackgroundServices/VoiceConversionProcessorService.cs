using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VoiceByAuribus_API.Features.VoiceConversions.Application.Services;
using VoiceByAuribus_API.Shared.Application.Dtos;
using VoiceByAuribus_API.Shared.Infrastructure.Data;
using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Features.VoiceConversions.Application.BackgroundServices;

/// <summary>
/// Background service that processes pending voice conversions every 3 seconds.
/// Uses Optimistic Locking to prevent race conditions when multiple API instances are running.
/// </summary>
public class VoiceConversionProcessorService : BackgroundService, IBackgroundServiceHealthCheck
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<VoiceConversionProcessorService> _logger;
    private readonly int _intervalSeconds;
    private readonly int _batchTimeoutSeconds;
    
    private DateTime? _lastSuccessfulRun;
    private int _totalProcessed;
    private int _totalSkipped;
    private bool _isRunning;

    public VoiceConversionProcessorService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<VoiceConversionProcessorService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _intervalSeconds = configuration.GetValue("VoiceConversions:BackgroundProcessor:IntervalSeconds", 3);
        _batchTimeoutSeconds = configuration.GetValue("VoiceConversions:BackgroundProcessor:BatchTimeoutSeconds", 40);
    }

    public BackgroundServiceHealthStatus GetHealthStatus()
    {
        var timeSinceLastRun = _lastSuccessfulRun.HasValue 
            ? DateTime.UtcNow - _lastSuccessfulRun.Value 
            : (TimeSpan?)null;

        var status = "healthy";
        string? message = null;

        if (!_isRunning)
        {
            status = "unhealthy";
            message = "Service is not running";
        }
        else if (timeSinceLastRun.HasValue && timeSinceLastRun.Value.TotalMinutes > 5)
        {
            status = "degraded";
            message = $"No successful run in the last {timeSinceLastRun.Value.TotalMinutes:F1} minutes";
        }

        return new BackgroundServiceHealthStatus
        {
            ServiceName = "VoiceConversionProcessor",
            IsRunning = _isRunning,
            LastSuccessfulRun = _lastSuccessfulRun,
            TotalProcessed = _totalProcessed,
            TotalSkipped = _totalSkipped,
            Status = status,
            Message = message
        };
    }

    public void RecordBatchProcessed(int processed, int skipped)
    {
        _totalProcessed += processed;
        _totalSkipped += skipped;
        _lastSuccessfulRun = DateTime.UtcNow;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "VoiceConversionProcessorService started. Processing every {Interval} seconds with {Timeout} second timeout",
            _intervalSeconds, _batchTimeoutSeconds);

        _isRunning = true;

        // Wait a bit before starting to allow the application to fully initialize
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingConversionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in VoiceConversionProcessorService main loop");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
                break;
            }
        }

        _isRunning = false;
        _logger.LogInformation("VoiceConversionProcessorService stopped");
    }

    private async Task ProcessPendingConversionsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var conversionService = scope.ServiceProvider.GetRequiredService<IVoiceConversionService>();

        // Create timeout cancellation token
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_batchTimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var (processed, skipped) = await conversionService.ProcessPendingConversionsAsync(linkedCts.Token);
            RecordBatchProcessed(processed, skipped);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Processing pending conversions timed out after {Timeout} seconds",
                _batchTimeoutSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing pending voice conversions");
        }
    }
}
