namespace VoiceByAuribus_API.Features.AudioFiles.Domain;

/// <summary>
/// Represents the upload status of an audio file to S3.
/// </summary>
public enum UploadStatus
{
    /// <summary>
    /// File record created in database, awaiting client upload to S3.
    /// </summary>
    AwaitingUpload = 0,

    /// <summary>
    /// File successfully uploaded to S3.
    /// </summary>
    Uploaded = 1,

    /// <summary>
    /// Upload failed or file verification failed.
    /// </summary>
    Failed = 2
}
