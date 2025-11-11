namespace VoiceByAuribus_API.Features.AudioFiles.Application.Dtos;

/// <summary>
/// DTO for S3 upload notification webhook payload.
/// </summary>
public class UploadNotificationDto
{
    public required string S3Uri { get; set; }
}
