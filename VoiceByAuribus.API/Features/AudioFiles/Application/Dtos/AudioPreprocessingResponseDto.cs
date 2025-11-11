using System;

namespace VoiceByAuribus_API.Features.AudioFiles.Application.Dtos;

/// <summary>
/// DTO for audio preprocessing information (admin-only).
/// </summary>
public class AudioPreprocessingResponseDto
{
    public required string Status { get; set; }
    public decimal? AudioDurationSeconds { get; set; }
    public string? S3UriShort { get; set; }
    public string? S3UriInference { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? ProcessingCompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
