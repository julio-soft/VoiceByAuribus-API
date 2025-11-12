using VoiceByAuribus_API.Features.AudioFiles.Application.Dtos;
using VoiceByAuribus_API.Features.AudioFiles.Domain;

namespace VoiceByAuribus_API.Features.AudioFiles.Application.Mappers;

/// <summary>
/// Mapper for AudioPreprocessing entity to DTOs.
/// </summary>
public static class AudioPreprocessingMapper
{
    /// <summary>
    /// Maps AudioPreprocessing entity to AudioPreprocessingResponseDto.
    /// </summary>
    /// <param name="preprocessing">The preprocessing entity to map</param>
    /// <returns>AudioPreprocessingResponseDto with preprocessing information</returns>
    public static AudioPreprocessingResponseDto MapToResponseDto(AudioPreprocessing preprocessing)
    {
        return new AudioPreprocessingResponseDto
        {
            Status = preprocessing.ProcessingStatus.ToString(),
            AudioDurationSeconds = preprocessing.AudioDurationSeconds,
            S3UriShort = preprocessing.S3UriShort,
            S3UriInference = preprocessing.S3UriInference,
            ProcessingStartedAt = preprocessing.ProcessingStartedAt,
            ProcessingCompletedAt = preprocessing.ProcessingCompletedAt,
            ErrorMessage = preprocessing.ErrorMessage
        };
    }
}
