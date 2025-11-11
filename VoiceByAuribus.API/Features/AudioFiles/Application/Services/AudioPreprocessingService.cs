using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using VoiceByAuribus_API.Features.AudioFiles.Application.Dtos;
using VoiceByAuribus_API.Features.AudioFiles.Domain;
using VoiceByAuribus_API.Shared.Infrastructure.Data;
using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Features.AudioFiles.Application.Services;

/// <summary>
/// Service for managing audio preprocessing operations.
/// </summary>
public class AudioPreprocessingService(
    ApplicationDbContext context,
    ISqsService sqsService,
    IDateTimeProvider dateTimeProvider,
    IConfiguration configuration) : IAudioPreprocessingService
{
    private readonly string _queueUrl = configuration["AWS:SQS:AudioPreprocessingQueueUrl"]
        ?? throw new InvalidOperationException("AWS:SQS:AudioPreprocessingQueueUrl configuration is required");

    private readonly string _audioBucket = configuration["AWS:S3:AudioFilesBucket"]
        ?? throw new InvalidOperationException("AWS:S3:AudioFilesBucket configuration is required");

    public async Task TriggerPreprocessingAsync(Guid audioFileId)
    {
        var audioFile = await context.AudioFiles
            .Include(af => af.Preprocessing)
            .FirstOrDefaultAsync(af => af.Id == audioFileId);

        if (audioFile is null)
        {
            throw new InvalidOperationException($"Audio file not found: {audioFileId}");
        }

        // Create preprocessing record if it doesn't exist
        if (audioFile.Preprocessing is null)
        {
            var preprocessing = new AudioPreprocessing
            {
                AudioFileId = audioFile.Id,
                ProcessingStatus = ProcessingStatus.Pending,
                S3UriShort = BuildS3UriShort(audioFile),
                S3UriInference = BuildS3UriInference(audioFile)
            };

            context.AudioPreprocessings.Add(preprocessing);
            await context.SaveChangesAsync();

            audioFile.Preprocessing = preprocessing;
        }

        // Update status to processing
        audioFile.Preprocessing.ProcessingStatus = ProcessingStatus.Processing;
        audioFile.Preprocessing.ProcessingStartedAt = dateTimeProvider.UtcNow;
        await context.SaveChangesAsync();

        // Send message to SQS
        var message = new PreprocessingMessageDto
        {
            S3KeyTemp = audioFile.S3Uri,
            S3KeyShort = audioFile.Preprocessing.S3UriShort!,
            S3KeyForInference = audioFile.Preprocessing.S3UriInference!
        };

        await sqsService.SendMessageAsync(_queueUrl, message);
    }

    public async Task HandlePreprocessingResultAsync(PreprocessingResultDto dto)
    {
        var audioFile = await context.AudioFiles
            .Include(af => af.Preprocessing)
            .FirstOrDefaultAsync(af => af.S3Uri == dto.S3KeyTemp);

        if (audioFile is null)
        {
            throw new InvalidOperationException($"Audio file not found for S3 URI: {dto.S3KeyTemp}");
        }

        if (audioFile.Preprocessing is null)
        {
            throw new InvalidOperationException($"Preprocessing record not found for audio file: {audioFile.Id}");
        }

        var preprocessing = audioFile.Preprocessing;

        if (dto.AudioDuration.HasValue)
        {
            // Success
            preprocessing.ProcessingStatus = ProcessingStatus.Completed;
            preprocessing.AudioDurationSeconds = dto.AudioDuration.Value;
        }
        else
        {
            // Failed
            preprocessing.ProcessingStatus = ProcessingStatus.Failed;
            preprocessing.ErrorMessage = "Audio duration not provided - preprocessing failed";
        }

        preprocessing.ProcessingCompletedAt = dateTimeProvider.UtcNow;
        await context.SaveChangesAsync();
    }

    public async Task<AudioPreprocessingResponseDto?> GetPreprocessingStatusAsync(Guid audioFileId)
    {
        var preprocessing = await context.AudioPreprocessings
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.AudioFileId == audioFileId);

        if (preprocessing is null)
        {
            return null;
        }

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

    private string BuildS3UriShort(AudioFile audioFile)
    {
        var userId = audioFile.UserId!.Value;
        var fileId = audioFile.Id;
        return $"s3://{_audioBucket}/audio-files/{userId}/short/{fileId}.mp3";
    }

    private string BuildS3UriInference(AudioFile audioFile)
    {
        var userId = audioFile.UserId!.Value;
        var fileId = audioFile.Id;
        return $"s3://{_audioBucket}/audio-files/{userId}/inference/{fileId}.mp3";
    }
}
