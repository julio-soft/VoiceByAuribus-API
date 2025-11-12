using System;

namespace VoiceByAuribus_API.Features.AudioFiles.Application.Dtos;

/// <summary>
/// DTO for response when creating a new audio file.
/// Includes upload URL for client to upload the file.
/// </summary>
public class AudioFileCreatedResponseDto
{
    public Guid Id { get; set; }
    public required string FileName { get; set; }
    public required string MimeType { get; set; }
    public required string UploadStatus { get; set; }
    public required string UploadUrl { get; set; }
    public DateTime UploadUrlExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
