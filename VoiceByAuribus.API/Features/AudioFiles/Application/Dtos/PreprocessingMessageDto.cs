namespace VoiceByAuribus_API.Features.AudioFiles.Application.Dtos;

/// <summary>
/// DTO for SQS message to trigger audio preprocessing.
/// </summary>
public class PreprocessingMessageDto
{
    public required string S3KeyTemp { get; set; }
    public required string S3KeyShort { get; set; }
    public required string S3KeyForInference { get; set; }
}
