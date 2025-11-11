using System;

namespace VoiceByAuribus_API.Features.AudioFiles.Application.Dtos;

/// <summary>
/// DTO for response when regenerating upload URL.
/// </summary>
public class RegenerateUploadUrlResponseDto
{
    public required string UploadUrl { get; set; }
    public DateTime UploadUrlExpiresAt { get; set; }
}
