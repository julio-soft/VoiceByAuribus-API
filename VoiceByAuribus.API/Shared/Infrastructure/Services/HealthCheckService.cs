using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.SQS;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VoiceByAuribus_API.Shared.Application.Dtos;
using VoiceByAuribus_API.Shared.Infrastructure.Data;
using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Shared.Infrastructure.Services;

/// <summary>
/// Service for performing health checks on critical application services
/// </summary>
public class HealthCheckService : IHealthCheckService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAmazonS3 _s3Client;
    private readonly IAmazonSQS _sqsClient;
    private readonly ILogger<HealthCheckService> _logger;

    public HealthCheckService(
        ApplicationDbContext dbContext,
        IAmazonS3 s3Client,
        IAmazonSQS sqsClient,
        ILogger<HealthCheckService> logger)
    {
        _dbContext = dbContext;
        _s3Client = s3Client;
        _sqsClient = sqsClient;
        _logger = logger;
    }

    public async Task<HealthCheckResponse> CheckHealthAsync()
    {
        var response = new HealthCheckResponse
        {
            Status = "healthy",
            Services = new Dictionary<string, ServiceHealthStatus>()
        };

        // Check Database (critical)
        var dbStatus = await CheckDatabaseAsync();
        response.Services["database"] = dbStatus;

        // Check AWS S3 (non-critical)
        var s3Status = await CheckS3Async();
        response.Services["s3"] = s3Status;

        // Check AWS SQS (non-critical)
        var sqsStatus = await CheckSqsAsync();
        response.Services["sqs"] = sqsStatus;

        // Determine overall health status
        // Only database is critical for overall health
        if (dbStatus.Status != "healthy")
        {
            response.Status = "unhealthy";
        }
        else if (s3Status.Status != "healthy" || sqsStatus.Status != "healthy")
        {
            response.Status = "degraded";
        }

        return response;
    }

    private async Task<ServiceHealthStatus> CheckDatabaseAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Fast query to check database connectivity
            await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1");
            stopwatch.Stop();

            return new ServiceHealthStatus
            {
                Status = "healthy",
                ResponseTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Database health check failed");
            return new ServiceHealthStatus
            {
                Status = "unhealthy",
                Message = "Database connection failed",
                ResponseTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    private async Task<ServiceHealthStatus> CheckS3Async()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Quick check to list buckets (minimal operation)
            await _s3Client.ListBucketsAsync();
            stopwatch.Stop();

            return new ServiceHealthStatus
            {
                Status = "healthy",
                ResponseTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "S3 health check failed");
            return new ServiceHealthStatus
            {
                Status = "unhealthy",
                Message = "S3 service unavailable",
                ResponseTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    private async Task<ServiceHealthStatus> CheckSqsAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Quick check to list queues (minimal operation)
            await _sqsClient.ListQueuesAsync(string.Empty);
            stopwatch.Stop();

            return new ServiceHealthStatus
            {
                Status = "healthy",
                ResponseTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "SQS health check failed");
            return new ServiceHealthStatus
            {
                Status = "unhealthy",
                Message = "SQS service unavailable",
                ResponseTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }
}
