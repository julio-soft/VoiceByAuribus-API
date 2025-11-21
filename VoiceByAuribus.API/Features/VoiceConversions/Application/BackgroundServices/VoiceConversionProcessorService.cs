using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VoiceByAuribus_API.Features.VoiceConversions.Application.Services;
using VoiceByAuribus_API.Shared.Infrastructure.Data;

namespace VoiceByAuribus_API.Features.VoiceConversions.Application.BackgroundServices;

/// <summary>
/// Background service that processes pending voice conversions every 3 seconds.
/// Uses Optimistic Locking to prevent race conditions when multiple API instances are running.
/// </summary>
public class VoiceConversionProcessorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<VoiceConversionProcessorService> _logger;
    private const int IntervalSeconds = 3;

    public VoiceConversionProcessorService(
        IServiceProvider serviceProvider,
        ILogger<VoiceConversionProcessorService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "VoiceConversionProcessorService started. Processing every {Interval} seconds",
            IntervalSeconds);

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
                await Task.Delay(TimeSpan.FromSeconds(IntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
                break;
            }
        }

        _logger.LogInformation("VoiceConversionProcessorService stopped");
    }

    private async Task ProcessPendingConversionsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var conversionService = scope.ServiceProvider.GetRequiredService<IVoiceConversionService>();

        try
        {
            await conversionService.ProcessPendingConversionsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing pending voice conversions");
        }
    }
}
