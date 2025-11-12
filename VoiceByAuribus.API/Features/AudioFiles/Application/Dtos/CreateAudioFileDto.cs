using System.ComponentModel.DataAnnotations;

namespace VoiceByAuribus_API.Features.AudioFiles.Application.Dtos;

/// <summary>
/// DTO for creating a new audio file record.
/// </summary>
public class CreateAudioFileDto
{
    [Required(ErrorMessage = "File name is required")]
    [StringLength(255, ErrorMessage = "File name must not exceed 255 characters")]
    public required string FileName { get; set; }

    [Required(ErrorMessage = "MIME type is required")]
    [RegularExpression(@"^audio\/.*", ErrorMessage = "Only audio files are allowed (MIME type must start with 'audio/')")]
    public required string MimeType { get; set; }
}
