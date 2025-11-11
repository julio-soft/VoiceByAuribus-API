using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using VoiceByAuribus_API.Features.AudioFiles.Application.Dtos;
using VoiceByAuribus_API.Features.AudioFiles.Domain;
using VoiceByAuribus_API.Shared.Infrastructure.Data;
using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Features.AudioFiles.Application.Services;

/// <summary>
/// Service for managing audio files.
/// </summary>
public class AudioFileService(
    ApplicationDbContext context,
    IS3PresignedUrlService presignedUrlService,
    IDateTimeProvider dateTimeProvider,
    IAudioPreprocessingService preprocessingService,
    IConfiguration configuration) : IAudioFileService
{
    private readonly string _audioBucket = configuration["AWS:S3:AudioFilesBucket"]
        ?? throw new InvalidOperationException("AWS:S3:AudioFilesBucket configuration is required");
    private readonly int _uploadExpirationMinutes = configuration.GetValue<int>("AWS:S3:UploadUrlExpirationMinutes");
    private readonly long _maxFileSizeBytes = configuration.GetValue<int>("AWS:S3:MaxFileSizeMB") * 1024 * 1024;

    public async Task<AudioFileCreatedResponseDto> CreateAudioFileAsync(CreateAudioFileDto dto, Guid userId)
    {
        var audioFile = new AudioFile
        {
            UserId = userId,
            FileName = dto.FileName,
            FileSize = dto.FileSize,
            MimeType = dto.MimeType,
            S3Uri = BuildS3Uri(userId, Guid.NewGuid(), dto.MimeType),
            UploadStatus = UploadStatus.AwaitingUpload
        };

        context.AudioFiles.Add(audioFile);
        await context.SaveChangesAsync();

        var uploadUrl = GenerateUploadUrl(audioFile.S3Uri, dto.MimeType);
        var expiresAt = dateTimeProvider.UtcNow.AddMinutes(_uploadExpirationMinutes);

        return new AudioFileCreatedResponseDto
        {
            Id = audioFile.Id,
            FileName = audioFile.FileName,
            FileSize = audioFile.FileSize,
            MimeType = audioFile.MimeType,
            UploadStatus = audioFile.UploadStatus.ToString(),
            UploadUrl = uploadUrl,
            UploadUrlExpiresAt = expiresAt,
            CreatedAt = audioFile.CreatedAt
        };
    }

    public async Task<RegenerateUploadUrlResponseDto> RegenerateUploadUrlAsync(Guid id, Guid userId)
    {
        var audioFile = await context.AudioFiles
            .FirstOrDefaultAsync(af => af.Id == id && af.UserId == userId);

        if (audioFile is null)
        {
            throw new InvalidOperationException("Audio file not found");
        }

        if (audioFile.UploadStatus != UploadStatus.AwaitingUpload)
        {
            throw new InvalidOperationException("Upload URL can only be regenerated for files awaiting upload");
        }

        var uploadUrl = GenerateUploadUrl(audioFile.S3Uri, audioFile.MimeType);
        var expiresAt = dateTimeProvider.UtcNow.AddMinutes(_uploadExpirationMinutes);

        return new RegenerateUploadUrlResponseDto
        {
            UploadUrl = uploadUrl,
            UploadUrlExpiresAt = expiresAt
        };
    }

    public async Task<AudioFileResponseDto?> GetAudioFileByIdAsync(Guid id, Guid userId, bool isAdmin)
    {
        var audioFile = await context.AudioFiles
            .Include(af => af.Preprocessing)
            .AsNoTracking()
            .FirstOrDefaultAsync(af => af.Id == id && af.UserId == userId);

        if (audioFile is null)
        {
            return null;
        }

        return MapToResponseDto(audioFile, isAdmin);
    }

    public async Task<(AudioFileResponseDto[] Items, int TotalCount)> GetUserAudioFilesAsync(Guid userId, int page, int pageSize)
    {
        var query = context.AudioFiles
            .Include(af => af.Preprocessing)
            .AsNoTracking()
            .Where(af => af.UserId == userId);

        var totalCount = await query.CountAsync();

        var audioFiles = await query
            .OrderByDescending(af => af.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = audioFiles.Select(af => MapToResponseDto(af, false)).ToArray();

        return (items, totalCount);
    }

    public async Task<bool> SoftDeleteAsync(Guid id, Guid userId)
    {
        var audioFile = await context.AudioFiles
            .FirstOrDefaultAsync(af => af.Id == id && af.UserId == userId);

        if (audioFile is null)
        {
            return false;
        }

        audioFile.IsDeleted = true;
        await context.SaveChangesAsync();

        return true;
    }

    public async Task HandleUploadNotificationAsync(string s3Uri)
    {
        var audioFile = await context.AudioFiles
            .FirstOrDefaultAsync(af => af.S3Uri == s3Uri);

        if (audioFile is null)
        {
            throw new InvalidOperationException($"Audio file not found for S3 URI: {s3Uri}");
        }

        audioFile.UploadStatus = UploadStatus.Uploaded;
        await context.SaveChangesAsync();

        // Trigger preprocessing
        await preprocessingService.TriggerPreprocessingAsync(audioFile.Id);
    }

    private string BuildS3Uri(Guid userId, Guid fileId, string mimeType)
    {
        var extension = GetExtensionFromMimeType(mimeType);
        return $"s3://{_audioBucket}/audio-files/{userId}/temp/{fileId}{extension}";
    }

    private string GenerateUploadUrl(string s3Uri, string contentType)
    {
        var (bucket, key) = ParseS3Uri(s3Uri);
        var lifetime = TimeSpan.FromMinutes(_uploadExpirationMinutes);
        return presignedUrlService.CreateUploadUrl(bucket, key, lifetime, _maxFileSizeBytes, contentType);
    }

    private AudioFileResponseDto MapToResponseDto(AudioFile audioFile, bool isAdmin)
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
                dto.Preprocessing = new AudioPreprocessingResponseDto
                {
                    Status = audioFile.Preprocessing.ProcessingStatus.ToString(),
                    AudioDurationSeconds = audioFile.Preprocessing.AudioDurationSeconds,
                    S3UriShort = audioFile.Preprocessing.S3UriShort,
                    S3UriInference = audioFile.Preprocessing.S3UriInference,
                    ProcessingStartedAt = audioFile.Preprocessing.ProcessingStartedAt,
                    ProcessingCompletedAt = audioFile.Preprocessing.ProcessingCompletedAt,
                    ErrorMessage = audioFile.Preprocessing.ErrorMessage
                };
            }
        }

        return dto;
    }

    private static string GetExtensionFromMimeType(string mimeType)
    {
        return mimeType.ToLowerInvariant() switch
        {
            "audio/mpeg" => ".mp3",
            "audio/mp3" => ".mp3",
            "audio/wav" => ".wav",
            "audio/wave" => ".wav",
            "audio/x-wav" => ".wav",
            "audio/ogg" => ".ogg",
            "audio/flac" => ".flac",
            "audio/aac" => ".aac",
            "audio/m4a" => ".m4a",
            "audio/x-m4a" => ".m4a",
            _ => ".mp3" // default fallback
        };
    }

    private static (string Bucket, string Key) ParseS3Uri(string s3Uri)
    {
        var uri = new Uri(s3Uri);
        var bucket = uri.Host;
        var key = uri.AbsolutePath.TrimStart('/');
        return (bucket, key);
    }
}
