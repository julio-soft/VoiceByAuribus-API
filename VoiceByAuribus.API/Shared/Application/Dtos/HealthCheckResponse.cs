using System.Collections.Generic;

namespace VoiceByAuribus_API.Shared.Application.Dtos;

/// <summary>
/// Response for health check endpoint
/// </summary>
public class HealthCheckResponse
{
    public string Status { get; set; } = "healthy";
    public Dictionary<string, ServiceHealthStatus> Services { get; set; } = new();
}

/// <summary>
/// Health status for individual services
/// </summary>
public class ServiceHealthStatus
{
    public string Status { get; set; } = "healthy";
    public string? Message { get; set; }
    public long? ResponseTimeMs { get; set; }
}
