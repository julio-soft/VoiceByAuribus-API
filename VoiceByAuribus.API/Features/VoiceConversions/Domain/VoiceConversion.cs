using System;
using VoiceByAuribus_API.Features.AudioFiles.Domain;
using VoiceByAuribus_API.Features.Voices.Domain;
using VoiceByAuribus_API.Shared.Domain;

namespace VoiceByAuribus_API.Features.VoiceConversions.Domain;

/// <summary>
/// Represents a voice conversion request that transforms an audio file using a specific voice model.
/// </summary>
public class VoiceConversion : BaseAuditableEntity, IHasUserId
{
    /// <summary>
    /// ID of the user who created this conversion request.
    /// For M2M tokens, this is the Cognito client_id.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// ID of the audio file to be converted.
    /// </summary>
    public Guid AudioFileId { get; set; }

    /// <summary>
    /// Navigation property to the audio file.
    /// </summary>
    public AudioFile AudioFile { get; set; } = null!;

    /// <summary>
    /// ID of the voice model to apply to the audio.
    /// </summary>
    public Guid VoiceModelId { get; set; }

    /// <summary>
    /// Navigation property to the voice model.
    /// </summary>
    public VoiceModel VoiceModel { get; set; } = null!;

    /// <summary>
    /// Transposition value in semitones to apply during conversion.
    /// </summary>
    public Transposition Transposition { get; set; }

    /// <summary>
    /// Whether this conversion uses the preview (short) audio instead of the full audio.
    /// </summary>
    public bool UsePreview { get; set; } = false;

    /// <summary>
    /// Current status of the conversion.
    /// </summary>
    public ConversionStatus Status { get; set; } = ConversionStatus.PendingPreprocessing;

    /// <summary>
    /// S3 URI where the converted audio output will be stored.
    /// Generated when the conversion is queued.
    /// </summary>
    public string? OutputS3Uri { get; set; }

    /// <summary>
    /// Timestamp when the conversion was queued to the external service.
    /// </summary>
    public DateTime? QueuedAt { get; set; }

    /// <summary>
    /// Timestamp when the conversion processing started.
    /// </summary>
    public DateTime? ProcessingStartedAt { get; set; }

    /// <summary>
    /// Timestamp when the conversion completed (successfully or with failure).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Error message if conversion failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of retry attempts for queuing this conversion (used by background processor).
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Timestamp of the last retry attempt.
    /// </summary>
    public DateTime? LastRetryAt { get; set; }

    /// <summary>
    /// Row version for optimistic concurrency control.
    /// Prevents multiple API instances from processing the same conversion simultaneously.
    /// </summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
