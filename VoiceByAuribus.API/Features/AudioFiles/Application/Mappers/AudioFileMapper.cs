using VoiceByAuribus_API.Features.AudioFiles.Application.Dtos;
using VoiceByAuribus_API.Features.AudioFiles.Domain;

namespace VoiceByAuribus_API.Features.AudioFiles.Application.Mappers;

/// <summary>
/// Mapper for AudioFile entity to DTOs.
/// </summary>
public static class AudioFileMapper
{
    /// <summary>
    /// Maps AudioFile entity to AudioFileResponseDto.
    /// </summary>
    /// <param name="audioFile">The audio file entity to map</param>
    /// <param name="isAdmin">Whether the current user is an admin (to include sensitive data)</param>
    /// <returns>AudioFileResponseDto with appropriate data based on admin status</returns>
    public static AudioFileResponseDto MapToResponseDto(AudioFile audioFile, bool isAdmin)
    {
        var dto = new AudioFileResponseDto
        {
            Id = audioFile.Id,
            FileName = audioFile.FileName,
            FileSize = audioFile.FileSize,
            MimeType = audioFile.MimeType,
            UploadStatus = audioFile.UploadStatus.ToString(),
            IsProcessed = audioFile.Preprocessing?.ProcessingStatus == ProcessingStatus.Completed,
            CreatedAt = audioFile.CreatedAt,
            UpdatedAt = audioFile.UpdatedAt
        };

        if (isAdmin)
        {
            dto.S3Uri = audioFile.S3Uri;

            if (audioFile.Preprocessing is not null)
            {
                dto.Preprocessing = AudioPreprocessingMapper.MapToResponseDto(audioFile.Preprocessing);
            }
        }

        return dto;
    }
}
