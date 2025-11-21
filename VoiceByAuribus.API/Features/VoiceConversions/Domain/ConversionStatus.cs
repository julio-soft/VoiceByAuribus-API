namespace VoiceByAuribus_API.Features.VoiceConversions.Domain;

/// <summary>
/// Represents the status of a voice conversion request.
/// </summary>
public enum ConversionStatus
{
    /// <summary>
    /// Conversion request created, waiting for audio preprocessing to complete.
    /// </summary>
    PendingPreprocessing = 0,

    /// <summary>
    /// Audio preprocessing completed, conversion queued for processing.
    /// </summary>
    Queued = 1,

    /// <summary>
    /// Conversion is currently being processed by the external service.
    /// </summary>
    Processing = 2,

    /// <summary>
    /// Conversion completed successfully.
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Conversion failed due to an error.
    /// </summary>
    Failed = 4
}
