namespace VoiceByAuribus_API.Features.AudioFiles.Application.Dtos;

/// <summary>
/// DTO for creating a new audio file record.
/// </summary>
public class CreateAudioFileDto
{
    public required string FileName { get; set; }
    public long FileSize { get; set; }
    public required string MimeType { get; set; }
}
