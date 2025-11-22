using System;

namespace VoiceByAuribus_API.Shared.Application.Dtos;

/// <summary>
/// Health status for background services
/// </summary>
public class BackgroundServiceHealthStatus
{
    /// <summary>
    /// Name of the background service
    /// </summary>
    public required string ServiceName { get; set; }

    /// <summary>
    /// Whether the service is running
    /// </summary>
    public required bool IsRunning { get; set; }

    /// <summary>
    /// Last successful execution time (UTC)
    /// </summary>
    public DateTime? LastSuccessfulRun { get; set; }

    /// <summary>
    /// Total items processed since startup
    /// </summary>
    public int TotalProcessed { get; set; }

    /// <summary>
    /// Total items skipped since startup
    /// </summary>
    public int TotalSkipped { get; set; }

    /// <summary>
    /// Health status: "healthy", "degraded", "unhealthy"
    /// </summary>
    public required string Status { get; set; }

    /// <summary>
    /// Optional message with additional details
    /// </summary>
    public string? Message { get; set; }
}
