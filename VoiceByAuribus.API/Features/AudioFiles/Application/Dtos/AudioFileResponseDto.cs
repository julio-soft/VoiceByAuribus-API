using System;

namespace VoiceByAuribus_API.Features.AudioFiles.Application.Dtos;

/// <summary>
/// DTO for audio file responses.
/// </summary>
public class AudioFileResponseDto
{
    public Guid Id { get; set; }
    public required string FileName { get; set; }
    public long? FileSize { get; set; }
    public required string MimeType { get; set; }
    public required string UploadStatus { get; set; }
    public bool IsProcessed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Admin-only fields
    public string? S3Uri { get; set; }
    public AudioPreprocessingResponseDto? Preprocessing { get; set; }
}
