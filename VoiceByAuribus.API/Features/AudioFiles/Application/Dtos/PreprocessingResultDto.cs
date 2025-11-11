namespace VoiceByAuribus_API.Features.AudioFiles.Application.Dtos;

/// <summary>
/// DTO for preprocessing result webhook payload.
/// </summary>
public class PreprocessingResultDto
{
    public required string S3KeyTemp { get; set; }
    public decimal? AudioDuration { get; set; }
}
