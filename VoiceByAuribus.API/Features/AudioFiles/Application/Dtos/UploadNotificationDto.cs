using System.ComponentModel.DataAnnotations;

namespace VoiceByAuribus_API.Features.AudioFiles.Application.Dtos;

/// <summary>
/// DTO for S3 upload notification webhook payload.
/// </summary>
public class UploadNotificationDto
{
    [Required]
    public required string S3Uri { get; set; }

    [Required]
    [Range(1, long.MaxValue, ErrorMessage = "File size must be greater than 0")]
    public long FileSize { get; set; }
}
