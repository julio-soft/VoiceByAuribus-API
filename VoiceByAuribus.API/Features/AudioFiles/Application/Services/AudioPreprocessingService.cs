using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VoiceByAuribus_API.Features.AudioFiles.Application.Dtos;
using VoiceByAuribus_API.Features.AudioFiles.Domain;
using VoiceByAuribus_API.Shared.Infrastructure.Data;
using VoiceByAuribus_API.Shared.Infrastructure.Services;
using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Features.AudioFiles.Application.Services;

/// <summary>
/// Service for managing audio preprocessing operations.
/// </summary>
public class AudioPreprocessingService(
    ApplicationDbContext context,
    ISqsService sqsService,
    SqsQueueResolver sqsQueueResolver,
    IDateTimeProvider dateTimeProvider,
    IConfiguration configuration,
    ILogger<AudioPreprocessingService> logger) : IAudioPreprocessingService
{
    private readonly string _queueName = configuration["AWS:SQS:AudioPreprocessingQueue"]
        ?? throw new InvalidOperationException("AWS:SQS:AudioPreprocessingQueue configuration is required");

    private readonly string _audioBucket = configuration["AWS:S3:AudioFilesBucket"]
        ?? throw new InvalidOperationException("AWS:S3:AudioFilesBucket configuration is required");

    private readonly string? _callbackUrl = configuration["AWS:SQS:PreprocessingCallbackUrl"];
    
    private readonly string _callbackType = configuration["AWS:SQS:PreprocessingCallbackType"] ?? "HTTP";

    public async Task TriggerPreprocessingAsync(Guid audioFileId)
    {
        logger.LogInformation(
            "Triggering audio preprocessing: AudioFileId={AudioFileId}",
            audioFileId);

        var audioFile = await context.AudioFiles
            .Include(af => af.Preprocessing)
            .FirstOrDefaultAsync(af => af.Id == audioFileId);

        if (audioFile is null)
        {
            logger.LogError(
                "Cannot trigger preprocessing - audio file not found: AudioFileId={AudioFileId}",
                audioFileId);
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
            
            logger.LogInformation(
                "Preprocessing record created: AudioFileId={AudioFileId}, S3UriShort={S3UriShort}, S3UriInference={S3UriInference}",
                audioFile.Id, preprocessing.S3UriShort, preprocessing.S3UriInference);
        }

        
        // Resolve queue URL from queue name
        var queueUrl = await sqsQueueResolver.GetQueueUrlAsync(_queueName);
        
        // Send message to SQS with request tracking and callback configuration
        var message = new PreprocessingMessageDto
        {
            S3KeyTemp = audioFile.S3Uri,
            S3KeyShort = audioFile.Preprocessing.S3UriShort!,
            S3KeyForInference = audioFile.Preprocessing.S3UriInference!,
            RequestId = audioFile.Id.ToString(),
            CallbackResponse = !string.IsNullOrEmpty(_callbackUrl) 
                ? new CallbackResponseDto 
                { 
                    Url = _callbackUrl, 
                    Type = _callbackType 
                } 
                : null
        };

        await sqsService.SendMessageAsync(queueUrl, message);

        // Update status to processing
        audioFile.Preprocessing.ProcessingStatus = ProcessingStatus.Processing;
        audioFile.Preprocessing.ProcessingStartedAt = dateTimeProvider.UtcNow;
        await context.SaveChangesAsync();
        
        logger.LogInformation(
            "Preprocessing message sent to SQS: AudioFileId={AudioFileId}, QueueName={QueueName}, QueueUrl={QueueUrl}",
            audioFileId, _queueName, queueUrl);
    }

    public async Task HandlePreprocessingResultAsync(PreprocessingResultDto dto)
    {
        logger.LogInformation(
            "Processing preprocessing result: S3KeyTemp={S3KeyTemp}, AudioDuration={AudioDuration}, Success={Success}, RequestId={RequestId}",
            dto.S3KeyTemp, dto.AudioDuration, dto.Success, dto.RequestId);

        var audioFile = await context.AudioFiles
            .Include(af => af.Preprocessing)
            .FirstOrDefaultAsync(af => af.S3Uri == dto.S3KeyTemp);

        if (audioFile is null)
        {
            logger.LogError(
                "Preprocessing result failed - audio file not found: S3KeyTemp={S3KeyTemp}",
                dto.S3KeyTemp);
            throw new InvalidOperationException($"Audio file not found for S3 URI: {dto.S3KeyTemp}");
        }

        // Validate request_id correlation if provided
        if (!string.IsNullOrEmpty(dto.RequestId) && dto.RequestId != audioFile.Id.ToString())
        {
            logger.LogWarning(
                "Request ID mismatch: Expected={ExpectedId}, Received={ReceivedId}",
                audioFile.Id, dto.RequestId);
        }

        if (audioFile.Preprocessing is null)
        {
            logger.LogError(
                "Preprocessing result failed - preprocessing record not found: AudioFileId={AudioFileId}",
                audioFile.Id);
            throw new InvalidOperationException($"Preprocessing record not found for audio file: {audioFile.Id}");
        }

        var preprocessing = audioFile.Preprocessing;

        // Use the explicit success field to determine processing outcome
        if (dto.Success && dto.AudioDuration.HasValue)
        {
            // Success
            preprocessing.ProcessingStatus = ProcessingStatus.Completed;
            preprocessing.AudioDurationSeconds = dto.AudioDuration.Value;
            
            logger.LogInformation(
                "Preprocessing completed successfully: AudioFileId={AudioFileId}, Duration={Duration}s",
                audioFile.Id, dto.AudioDuration.Value);
        }
        else
        {
            // Failed - either success=false or no audio_duration provided
            preprocessing.ProcessingStatus = ProcessingStatus.Failed;
            preprocessing.ErrorMessage = dto.Success 
                ? "Audio duration not provided despite success flag - preprocessing incomplete"
                : "Preprocessing service reported failure";
            
            logger.LogWarning(
                "Preprocessing failed: AudioFileId={AudioFileId}, Success={Success}, Reason={Reason}",
                audioFile.Id, dto.Success, preprocessing.ErrorMessage);
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
