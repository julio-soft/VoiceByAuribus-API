namespace VoiceByAuribus_API.Features.AudioFiles.Domain;

/// <summary>
/// Represents the preprocessing status of an audio file.
/// </summary>
public enum ProcessingStatus
{
    /// <summary>
    /// Preprocessing has been queued but not started yet.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Preprocessing is currently in progress.
    /// </summary>
    Processing = 1,

    /// <summary>
    /// Preprocessing completed successfully.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Preprocessing failed due to an error.
    /// </summary>
    Failed = 3
}
