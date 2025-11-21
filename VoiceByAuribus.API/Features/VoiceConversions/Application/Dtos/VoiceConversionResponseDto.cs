using System;
using VoiceByAuribus_API.Features.VoiceConversions.Domain;

namespace VoiceByAuribus_API.Features.VoiceConversions.Application.Dtos;

/// <summary>
/// DTO for voice conversion response.
/// </summary>
public class VoiceConversionResponseDto
{
    /// <summary>
    /// Unique identifier for the conversion.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID of the audio file being converted.
    /// </summary>
    public Guid AudioFileId { get; set; }

    /// <summary>
    /// Name of the audio file being converted.
    /// </summary>
    public string AudioFileName { get; set; } = string.Empty;

    /// <summary>
    /// ID of the voice model being applied.
    /// </summary>
    public Guid VoiceModelId { get; set; }

    /// <summary>
    /// Name of the voice model being applied.
    /// </summary>
    public string VoiceModelName { get; set; } = string.Empty;

    /// <summary>
    /// Pitch shift applied to the conversion.
    /// Values: "same_octave", "lower_octave", "higher_octave", "third_down", "third_up", "fifth_down", "fifth_up".
    /// </summary>
    public string PitchShift { get; set; } = string.Empty;

    /// <summary>
    /// Whether this conversion uses preview (short) audio.
    /// </summary>
    public bool UsePreview { get; set; }

    /// <summary>
    /// Current status of the conversion.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Pre-signed URL for downloading the converted audio (only when Status=Completed).
    /// </summary>
    public string? OutputUrl { get; set; }

    /// <summary>
    /// S3 URI of the output file (admin-only).
    /// </summary>
    public string? OutputS3Uri { get; set; }

    /// <summary>
    /// Timestamp when the conversion was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when the conversion was queued to the external service.
    /// </summary>
    public DateTime? QueuedAt { get; set; }

    /// <summary>
    /// Timestamp when the conversion processing started.
    /// </summary>
    public DateTime? ProcessingStartedAt { get; set; }

    /// <summary>
    /// Timestamp when the conversion completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Error message if conversion failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of retry attempts (admin-only).
    /// </summary>
    public int? RetryCount { get; set; }
}
