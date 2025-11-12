using System;
using VoiceByAuribus_API.Shared.Domain;

namespace VoiceByAuribus_API.Features.AudioFiles.Domain;

/// <summary>
/// Represents the preprocessing status and results for an audio file.
/// This includes information about the generated short preview and inference-ready file.
/// </summary>
public class AudioPreprocessing : BaseAuditableEntity
{
    /// <summary>
    /// ID of the associated audio file.
    /// </summary>
    public Guid AudioFileId { get; set; }

    /// <summary>
    /// Navigation property to the associated audio file.
    /// </summary>
    public AudioFile AudioFile { get; set; } = null!;

    /// <summary>
    /// Current processing status.
    /// </summary>
    public ProcessingStatus ProcessingStatus { get; set; } = ProcessingStatus.Pending;

    /// <summary>
    /// S3 URI of the generated 10-second preview/short audio file.
    /// </summary>
    public string? S3UriShort { get; set; }

    /// <summary>
    /// S3 URI of the processed audio file ready for inference.
    /// </summary>
    public string? S3UriInference { get; set; }

    /// <summary>
    /// Duration of the processed audio in seconds.
    /// Null if processing failed or not yet completed.
    /// </summary>
    public int? AudioDurationSeconds { get; set; }

    /// <summary>
    /// Timestamp when preprocessing started.
    /// </summary>
    public DateTime? ProcessingStartedAt { get; set; }

    /// <summary>
    /// Timestamp when preprocessing completed (successfully or with failure).
    /// </summary>
    public DateTime? ProcessingCompletedAt { get; set; }

    /// <summary>
    /// Error message if preprocessing failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
