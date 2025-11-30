using System;
using System.Threading.Tasks;
using VoiceByAuribus_API.Features.AudioFiles.Application.Dtos;

namespace VoiceByAuribus_API.Features.AudioFiles.Application.Services;

/// <summary>
/// Service for managing audio files.
/// </summary>
public interface IAudioFileService
{
    /// <summary>
    /// Creates a new audio file record and returns upload URL.
    /// </summary>
    Task<AudioFileCreatedResponseDto> CreateAudioFileAsync(CreateAudioFileDto dto, string userId);

    /// <summary>
    /// Regenerates upload URL for an audio file that hasn't been uploaded yet.
    /// </summary>
    Task<RegenerateUploadUrlResponseDto> RegenerateUploadUrlAsync(Guid id, string userId);

    /// <summary>
    /// Gets an audio file by ID.
    /// </summary>
    Task<AudioFileResponseDto?> GetAudioFileByIdAsync(Guid id, string userId, bool isAdmin);

    /// <summary>
    /// Gets paginated list of user's audio files.
    /// </summary>
    Task<(AudioFileResponseDto[] Items, int TotalCount)> GetUserAudioFilesAsync(string userId, int page, int pageSize);

    /// <summary>
    /// Soft deletes an audio file.
    /// </summary>
    Task<bool> SoftDeleteAsync(Guid id, string userId);

    /// <summary>
    /// Handles upload notification from S3 event.
    /// </summary>
    Task HandleUploadNotificationAsync(string s3Uri, long fileSize);
}
