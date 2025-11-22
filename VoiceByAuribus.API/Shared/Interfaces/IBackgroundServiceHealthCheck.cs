using System;
using VoiceByAuribus_API.Shared.Application.Dtos;

namespace VoiceByAuribus_API.Shared.Interfaces;

/// <summary>
/// Interface for monitoring background service health
/// </summary>
public interface IBackgroundServiceHealthCheck
{
    /// <summary>
    /// Gets the health status of the background service
    /// </summary>
    BackgroundServiceHealthStatus GetHealthStatus();

    /// <summary>
    /// Updates metrics after processing a batch
    /// </summary>
    void RecordBatchProcessed(int processed, int skipped);
}
