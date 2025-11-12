using System;
using VoiceByAuribus_API.Shared.Domain;

namespace VoiceByAuribus_API.Features.AudioFiles.Domain;

/// <summary>
/// Represents an audio file uploaded by a user for voice inference.
/// </summary>
public class AudioFile : BaseAuditableEntity, IHasUserId
{
    /// <summary>
    /// ID of the user who owns this audio file.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Original filename provided by the user.
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    /// File size in bytes. Set when file is uploaded to S3.
    /// </summary>
    public long? FileSize { get; set; }

    /// <summary>
    /// MIME type of the audio file (e.g., "audio/mpeg", "audio/wav").
    /// </summary>
    public required string MimeType { get; set; }

    /// <summary>
    /// S3 URI where the audio file is stored (e.g., "s3://bucket/key").
    /// </summary>
    public required string S3Uri { get; set; }

    /// <summary>
    /// Current upload status of the file.
    /// </summary>
    public UploadStatus UploadStatus { get; set; } = UploadStatus.AwaitingUpload;

    /// <summary>
    /// Navigation property to the preprocessing information for this audio file.
    /// </summary>
    public AudioPreprocessing? Preprocessing { get; set; }
}
