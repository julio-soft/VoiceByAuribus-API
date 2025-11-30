namespace VoiceByAuribus_API.Features.AudioFiles.Application.Dtos;

/// <summary>
/// DTO for preprocessing result webhook payload.
/// </summary>
public class PreprocessingResultDto
{
    /// <summary>
    /// Full S3 URI of the original temporary uploaded file.
    /// Format: s3://bucket/audio-files/{userId}/temp/{fileId}.ext
    /// Used to correlate the result with the original AudioFile record.
    /// </summary>
    public required string S3KeyTemp { get; set; }
    
    /// <summary>
    /// Audio duration in seconds. Null on failure.
    /// </summary>
    public int? AudioDuration { get; set; }
    
    /// <summary>
    /// True if processing succeeded, false otherwise.
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Original request ID if provided in the request.
    /// Used for correlating requests with responses.
    /// </summary>
    public string? RequestId { get; set; }
}
