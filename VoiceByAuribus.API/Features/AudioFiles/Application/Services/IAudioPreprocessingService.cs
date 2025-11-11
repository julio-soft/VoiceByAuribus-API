using System;
using System.Threading.Tasks;
using VoiceByAuribus_API.Features.AudioFiles.Application.Dtos;

namespace VoiceByAuribus_API.Features.AudioFiles.Application.Services;

/// <summary>
/// Service for managing audio preprocessing operations.
/// </summary>
public interface IAudioPreprocessingService
{
    /// <summary>
    /// Triggers preprocessing for an audio file by sending message to SQS.
    /// </summary>
    Task TriggerPreprocessingAsync(Guid audioFileId);

    /// <summary>
    /// Handles preprocessing result from webhook callback.
    /// </summary>
    Task HandlePreprocessingResultAsync(PreprocessingResultDto dto);

    /// <summary>
    /// Gets preprocessing status for an audio file.
    /// </summary>
    Task<AudioPreprocessingResponseDto?> GetPreprocessingStatusAsync(Guid audioFileId);
}
