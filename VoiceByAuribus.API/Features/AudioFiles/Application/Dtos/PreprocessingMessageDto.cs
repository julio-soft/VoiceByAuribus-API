namespace VoiceByAuribus_API.Features.AudioFiles.Application.Dtos;

/// <summary>
/// DTO for SQS message to trigger audio preprocessing.
/// </summary>
public class PreprocessingMessageDto
{
    /// <summary>
    /// Full S3 URI for the temporary (original) uploaded audio file.
    /// Format: s3://bucket/audio-files/{userId}/temp/{fileId}.ext
    /// </summary>
    public required string S3KeyTemp { get; set; }
    
    /// <summary>
    /// Full S3 URI where the short preview audio will be stored.
    /// Format: s3://bucket/audio-files/{userId}/short/{fileId}.mp3
    /// </summary>
    public required string S3KeyShort { get; set; }
    
    /// <summary>
    /// Full S3 URI where the inference-ready audio will be stored.
    /// Format: s3://bucket/audio-files/{userId}/inference/{fileId}.mp3
    /// </summary>
    public required string S3KeyForInference { get; set; }
    
    /// <summary>
    /// Optional unique identifier for tracking this request.
    /// Will be returned in the callback response for correlation.
    /// </summary>
    public string? RequestId { get; set; }
    
    /// <summary>
    /// Optional callback configuration for receiving processing results.
    /// </summary>
    public CallbackResponseDto? CallbackResponse { get; set; }
}

/// <summary>
/// Configuration for callback response delivery.
/// </summary>
public class CallbackResponseDto
{
    /// <summary>
    /// Destination URL for callback notification.
    /// For HTTP: Full URL (e.g., https://api.example.com/webhooks/preprocessing-result)
    /// For SQS: Full queue URL (e.g., https://sqs.region.amazonaws.com/account/queue-name)
    /// </summary>
    public required string Url { get; set; }
    
    /// <summary>
    /// Type of callback: "HTTP" for webhook POST or "SQS" for queue message.
    /// </summary>
    public required string Type { get; set; }
}
