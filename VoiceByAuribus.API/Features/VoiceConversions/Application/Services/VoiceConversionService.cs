using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VoiceByAuribus_API.Features.AudioFiles.Domain;
using VoiceByAuribus_API.Features.VoiceConversions.Application.Dtos;
using VoiceByAuribus_API.Features.VoiceConversions.Application.Helpers;
using VoiceByAuribus_API.Features.VoiceConversions.Application.Mappers;
using VoiceByAuribus_API.Features.VoiceConversions.Domain;
using VoiceByAuribus_API.Shared.Infrastructure.Data;
using VoiceByAuribus_API.Shared.Infrastructure.Services;
using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Features.VoiceConversions.Application.Services;

/// <summary>
/// Service for managing voice conversion operations.
/// </summary>
public class VoiceConversionService(
    ApplicationDbContext context,
    ISqsService sqsService,
    SqsQueueResolver sqsQueueResolver,
    IS3PresignedUrlService presignedUrlService,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IConfiguration configuration,
    ILogger<VoiceConversionService> logger) : IVoiceConversionService
{
    private readonly string _inferenceQueueName = configuration["AWS:SQS:VoiceInferenceQueue"]
        ?? throw new InvalidOperationException("AWS:SQS:VoiceInferenceQueue configuration is required");

    private readonly string _previewInferenceQueueName = configuration["AWS:SQS:PreviewInferenceQueue"]
        ?? throw new InvalidOperationException("AWS:SQS:PreviewInferenceQueue configuration is required");

    private readonly string _audioBucket = configuration["AWS:S3:AudioFilesBucket"]
        ?? throw new InvalidOperationException("AWS:S3:AudioFilesBucket configuration is required");


    private const int MaxRetryAttempts = 5;
    private const int RetryDelayMinutes = 5;

    public async Task<VoiceConversionResponseDto> CreateVoiceConversionAsync(CreateVoiceConversionDto dto, Guid userId)
    {
        logger.LogInformation(
            "Creating voice conversion: AudioFileId={AudioFileId}, VoiceModelId={VoiceModelId}, PitchShift={PitchShift}, UsePreview={UsePreview}, UserId={UserId}",
            dto.AudioFileId, dto.VoiceModelId, dto.PitchShift, dto.UsePreview, userId);

        // Convert pitch shift string to internal Transposition enum
        var transposition = PitchShiftHelper.ToTransposition(dto.PitchShift);

        // Validate audio file exists and belongs to user
        var audioFile = await context.AudioFiles
            .Include(af => af.Preprocessing)
            .FirstOrDefaultAsync(af => af.Id == dto.AudioFileId && af.UserId == userId);

        if (audioFile is null)
        {
            logger.LogWarning(
                "Audio file not found or unauthorized: AudioFileId={AudioFileId}, UserId={UserId}",
                dto.AudioFileId, userId);
            throw new InvalidOperationException($"Audio file not found: {dto.AudioFileId}");
        }

        if (audioFile.UploadStatus != UploadStatus.Uploaded)
        {
            logger.LogWarning(
                "Audio file upload not completed: AudioFileId={AudioFileId}, UploadStatus={UploadStatus}",
                dto.AudioFileId, audioFile.UploadStatus);
            throw new InvalidOperationException($"Audio file upload not completed: {dto.AudioFileId}");
        }

        // Validate voice model exists
        var voiceModel = await context.VoiceModels
            .AsNoTracking()
            .FirstOrDefaultAsync(vm => vm.Id == dto.VoiceModelId);

        if (voiceModel is null)
        {
            logger.LogWarning(
                "Voice model not found: VoiceModelId={VoiceModelId}",
                dto.VoiceModelId);
            throw new InvalidOperationException($"Voice model not found: {dto.VoiceModelId}");
        }

        // Create conversion record
        var conversionId = Guid.NewGuid();
        var conversion = new VoiceConversion
        {
            Id = conversionId,
            UserId = userId,
            AudioFileId = dto.AudioFileId,
            VoiceModelId = dto.VoiceModelId,
            Transposition = transposition,
            UsePreview = dto.UsePreview,
            OutputS3Uri = BuildOutputS3Uri(conversionId, userId, audioFile.FileName, transposition, dto.UsePreview),
            Status = ConversionStatus.PendingPreprocessing
        };

        // Check preprocessing status and determine initial state
        if (audioFile.Preprocessing is null)
        {
            logger.LogInformation(
                "Audio file has no preprocessing record yet: AudioFileId={AudioFileId}",
                dto.AudioFileId);
            conversion.Status = ConversionStatus.PendingPreprocessing;
        }
        else
        {
            switch (audioFile.Preprocessing.ProcessingStatus)
            {
                case ProcessingStatus.Failed:
                    logger.LogWarning(
                        "Cannot create conversion - audio preprocessing failed: AudioFileId={AudioFileId}",
                        dto.AudioFileId);
                    throw new InvalidOperationException(
                        $"Audio file preprocessing failed: {audioFile.Preprocessing.ErrorMessage ?? "Unknown error"}");

                case ProcessingStatus.Completed:
                    // Queue immediately
                    logger.LogInformation(
                        "Audio preprocessing completed - queueing conversion immediately: AudioFileId={AudioFileId}",
                        dto.AudioFileId);
                    conversion.Status = ConversionStatus.Queued;
                    break;

                case ProcessingStatus.Pending:
                case ProcessingStatus.Processing:
                    // Wait for preprocessing to complete
                    logger.LogInformation(
                        "Audio preprocessing in progress - conversion will be queued later: AudioFileId={AudioFileId}, PreprocessingStatus={Status}",
                        dto.AudioFileId, audioFile.Preprocessing.ProcessingStatus);
                    conversion.Status = ConversionStatus.PendingPreprocessing;
                    break;
            }
        }


        // If preprocessing is completed, send to queue immediately
        if (conversion.Status == ConversionStatus.Queued)
        {
            await QueueConversionAsync(conversion, voiceModel, audioFile);
            conversion.QueuedAt = dateTimeProvider.UtcNow;
        }

        context.VoiceConversions.Add(conversion);
        await context.SaveChangesAsync();

        logger.LogInformation(
            "Voice conversion created: ConversionId={ConversionId}, Status={Status}",
            conversion.Id, conversion.Status);

        // Reload with navigation properties
        var createdConversion = await context.VoiceConversions
            .Include(c => c.AudioFile)
            .Include(c => c.VoiceModel)
            .FirstAsync(c => c.Id == conversion.Id);

        return VoiceConversionMapper.MapToResponseDto(
            createdConversion,
            presignedUrlService,
            currentUserService.IsAdmin);
    }

    public async Task<VoiceConversionResponseDto?> GetVoiceConversionAsync(Guid conversionId, Guid userId)
    {
        logger.LogInformation(
            "Fetching voice conversion: ConversionId={ConversionId}, UserId={UserId}",
            conversionId, userId);

        var conversion = await context.VoiceConversions
            .Include(c => c.AudioFile)
            .Include(c => c.VoiceModel)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversionId && c.UserId == userId);

        if (conversion is null)
        {
            logger.LogWarning(
                "Voice conversion not found or unauthorized: ConversionId={ConversionId}, UserId={UserId}",
                conversionId, userId);
            return null;
        }

        return VoiceConversionMapper.MapToResponseDto(
            conversion,
            presignedUrlService,
            currentUserService.IsAdmin);
    }

    public async Task HandleConversionResultAsync(VoiceConversionWebhookDto dto)
    {
        logger.LogInformation(
            "Processing conversion result: InferenceId={InferenceId}, Status={Status}",
            dto.InferenceId, dto.Status);

        var conversion = await context.VoiceConversions
            .FirstOrDefaultAsync(c => c.Id == dto.InferenceId);

        if (conversion is null)
        {
            logger.LogError(
                "Conversion result failed - conversion not found: InferenceId={InferenceId}",
                dto.InferenceId);
            throw new InvalidOperationException($"Voice conversion not found: {dto.InferenceId}");
        }

        var isSuccess = dto.Status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase);

        if (isSuccess)
        {
            conversion.Status = ConversionStatus.Completed;
            conversion.CompletedAt = dateTimeProvider.UtcNow;

            logger.LogInformation(
                "Voice conversion completed successfully: ConversionId={ConversionId}",
                conversion.Id);
        }
        else
        {
            conversion.Status = ConversionStatus.Failed;
            conversion.ErrorMessage = dto.ErrorMessage ?? "External service reported failure";
            conversion.CompletedAt = dateTimeProvider.UtcNow;

            logger.LogWarning(
                "Voice conversion failed: ConversionId={ConversionId}, Error={Error}",
                conversion.Id, conversion.ErrorMessage);
        }

        await context.SaveChangesAsync();
    }

    public async Task<(int Processed, int Skipped)> ProcessPendingConversionsAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting background processing of pending voice conversions");

        var totalProcessed = 0;
        var totalSkipped = 0;
        var batchCount = 0;
        var hasMorePending = true;

        // Process all pending conversions in batches until no more are found
        while (hasMorePending)
        {
            batchCount++;

            // Find conversions pending preprocessing that haven't exceeded retry limit
            // Using AsNoTracking for initial read (Optimistic Locking pattern)
            var pendingConversionIds = await context.VoiceConversions
                .AsNoTracking()
                .Where(c => c.Status == ConversionStatus.PendingPreprocessing)
                .Where(c => c.RetryCount < MaxRetryAttempts)
                .Where(c => c.LastRetryAt == null ||
                           c.LastRetryAt.Value.AddMinutes(RetryDelayMinutes) <= dateTimeProvider.UtcNow)
                .Select(c => c.Id)
                .Take(10) // Process max 10 conversions per batch to avoid long-running transactions
                .ToListAsync(cancellationToken);

            if (pendingConversionIds.Count == 0)
            {
                hasMorePending = false;
                break;
            }

            logger.LogInformation(
                "Processing batch {BatchNumber}: Found {Count} pending conversions",
                batchCount, pendingConversionIds.Count);

            var batchProcessed = 0;
            var batchSkipped = 0;

            // Process each conversion individually with Optimistic Locking
            foreach (var conversionId in pendingConversionIds)
            {
                try
                {
                    var processed = await ProcessSingleConversionAsync(conversionId, cancellationToken);
                    if (processed)
                        batchProcessed++;
                    else
                        batchSkipped++;
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Another instance already processed this conversion - this is expected and OK
                    logger.LogDebug(
                        "Concurrency conflict for ConversionId={ConversionId} - another instance is processing it",
                        conversionId);
                    batchSkipped++;
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Error processing pending conversion: ConversionId={ConversionId}",
                        conversionId);
                    batchSkipped++;
                }
            }

            totalProcessed += batchProcessed;
            totalSkipped += batchSkipped;

            logger.LogInformation(
                "Batch {BatchNumber} completed: Processed={Processed}, Skipped={Skipped}",
                batchCount, batchProcessed, batchSkipped);

            // If we processed less than 10, it means there are no more pending
            // (or all remaining ones are being processed by other instances)
            if (pendingConversionIds.Count < 10)
            {
                hasMorePending = false;
            }
        }

        if (totalProcessed == 0 && totalSkipped == 0)
        {
            logger.LogInformation("No pending conversions to process");
        }
        else
        {
            logger.LogInformation(
                "Background processing completed: TotalBatches={Batches}, TotalProcessed={Processed}, TotalSkipped={Skipped}",
                batchCount, totalProcessed, totalSkipped);
        }

        return (totalProcessed, totalSkipped);
    }

    /// <summary>
    /// Processes a single conversion with Optimistic Locking to prevent race conditions.
    /// Returns true if processed, false if skipped.
    /// </summary>
    private async Task<bool> ProcessSingleConversionAsync(Guid conversionId, CancellationToken cancellationToken = default)
    {
        // Load conversion with related data in a new transaction
        var conversion = await context.VoiceConversions
            .Include(c => c.AudioFile)
                .ThenInclude(af => af.Preprocessing)
            .Include(c => c.VoiceModel)
            .FirstOrDefaultAsync(c => c.Id == conversionId, cancellationToken);

        if (conversion is null)
        {
            logger.LogWarning("Conversion not found: ConversionId={ConversionId}", conversionId);
            return false;
        }

        // Double-check status (might have changed since initial query)
        if (conversion.Status != ConversionStatus.PendingPreprocessing)
        {
            logger.LogDebug(
                "Conversion status changed, skipping: ConversionId={ConversionId}, Status={Status}",
                conversionId, conversion.Status);
            return false;
        }

        // Update retry tracking
        conversion.RetryCount++;
        conversion.LastRetryAt = dateTimeProvider.UtcNow;

        var preprocessing = conversion.AudioFile.Preprocessing;

        if (preprocessing is null)
        {
            logger.LogInformation(
                "Conversion still waiting for preprocessing to start: ConversionId={ConversionId}, RetryCount={RetryCount}",
                conversion.Id, conversion.RetryCount);

            await context.SaveChangesAsync(); // Save retry count increment
            return true;
        }

        switch (preprocessing.ProcessingStatus)
        {
            case ProcessingStatus.Failed:
                // Fail the conversion
                conversion.Status = ConversionStatus.Failed;
                conversion.ErrorMessage = $"Audio preprocessing failed: {preprocessing.ErrorMessage}";
                conversion.CompletedAt = dateTimeProvider.UtcNow;

                logger.LogWarning(
                    "Conversion failed due to preprocessing failure: ConversionId={ConversionId}",
                    conversion.Id);
                break;

            case ProcessingStatus.Completed:
                // Queue to SQS with deduplication
                await QueueConversionAsync(conversion, conversion.VoiceModel, conversion.AudioFile);
                
                // Queue the conversion
                conversion.Status = ConversionStatus.Queued;
                conversion.QueuedAt = dateTimeProvider.UtcNow;

                logger.LogInformation(
                    "Queueing conversion after preprocessing completed: ConversionId={ConversionId}",
                    conversion.Id);

                // SaveChanges first (updates RowVersion), then queue to SQS
                await context.SaveChangesAsync(cancellationToken);

                return true;

            case ProcessingStatus.Pending:
            case ProcessingStatus.Processing:
                // Still waiting
                logger.LogInformation(
                    "Conversion still waiting for preprocessing: ConversionId={ConversionId}, PreprocessingStatus={Status}, RetryCount={RetryCount}",
                    conversion.Id, preprocessing.ProcessingStatus, conversion.RetryCount);
                break;
        }

        // Check if max retries exceeded
        if (conversion.RetryCount >= MaxRetryAttempts &&
            conversion.Status == ConversionStatus.PendingPreprocessing)
        {
            conversion.Status = ConversionStatus.Failed;
            conversion.ErrorMessage = $"Max retry attempts ({MaxRetryAttempts}) exceeded";
            conversion.CompletedAt = dateTimeProvider.UtcNow;

            logger.LogWarning(
                "Conversion failed - max retries exceeded: ConversionId={ConversionId}",
                conversion.Id);
        }

        // Save changes with Optimistic Locking
        // If another instance modified this record, DbUpdateConcurrencyException will be thrown
        await context.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task QueueConversionAsync(
        VoiceConversion conversion,
        VoiceByAuribus_API.Features.Voices.Domain.VoiceModel voiceModel,
        VoiceByAuribus_API.Features.AudioFiles.Domain.AudioFile audioFile)
    {
        logger.LogInformation(
            "Sending conversion to SQS queue: ConversionId={ConversionId}, UsePreview={UsePreview}",
            conversion.Id, conversion.UsePreview);

        // Select the appropriate S3 URI based on UsePreview
        string inputS3Uri;
        if (conversion.UsePreview)
        {
            // Use the short/preview audio
            if (audioFile.Preprocessing?.S3UriShort is null)
            {
                throw new InvalidOperationException(
                    $"Audio preprocessing S3UriShort is null for AudioFileId={audioFile.Id}");
            }
            inputS3Uri = audioFile.Preprocessing.S3UriShort;
        }
        else
        {
            // Use the full inference audio
            if (audioFile.Preprocessing?.S3UriInference is null)
            {
                throw new InvalidOperationException(
                    $"Audio preprocessing S3UriInference is null for AudioFileId={audioFile.Id}");
            }
            inputS3Uri = audioFile.Preprocessing.S3UriInference;
        }

        var message = new
        {
            inference_id = conversion.Id,
            order_id = -1,
            voice_model_path = voiceModel.VoiceModelPath,
            voice_model_index_path = voiceModel.VoiceModelIndexPath,
            transposition = (int)conversion.Transposition,
            s3_key_for_inference = inputS3Uri,
            s3_key_out = conversion.OutputS3Uri
        };

        // Use conversion ID as deduplication ID to prevent duplicate messages
        // This ensures that even if multiple instances try to queue the same conversion,
        // only one message will be delivered to SQS (within 5-minute deduplication window for FIFO queues)
        var deduplicationId = conversion.Id.ToString();

        // Select the appropriate queue name based on UsePreview
        var queueName = conversion.UsePreview ? _previewInferenceQueueName : _inferenceQueueName;
        
        // Resolve queue URL from queue name
        var queueUrl = await sqsQueueResolver.GetQueueUrlAsync(queueName);

        await sqsService.SendMessageAsync(
            queueUrl,
            message,
            deduplicationId);

        logger.LogInformation(
            "Conversion message sent to SQS with deduplication: ConversionId={ConversionId}, DeduplicationId={DeduplicationId}, QueueName={QueueName}, QueueUrl={QueueUrl}, UsePreview={UsePreview}",
            conversion.Id, deduplicationId, queueName, queueUrl, conversion.UsePreview);
    }

    private string BuildOutputS3Uri(Guid conversionId, Guid userId, string fileName, Transposition transposition, bool isPreview = false)
    {
        var sanitizedFileName = SanitizeFileName(fileName);
        return $"s3://{_audioBucket}/audio-files/{userId}/converted/{sanitizedFileName}_{PitchShiftHelper.ToPitchShiftString(transposition)}{(isPreview ? "_preview" : "")}_{conversionId}.wav";
    }

    /// <summary>
    /// Sanitizes a filename to be S3-compatible by replacing invalid characters with underscores.
    /// Allows only alphanumeric characters, dots, hyphens, and underscores.
    /// </summary>
    /// <param name="fileName">The original filename (without extension)</param>
    /// <returns>Sanitized filename safe for S3 URIs</returns>
    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "file";
        }

        // Replace any character that is NOT alphanumeric, dot, hyphen, or underscore with an underscore
        // This prevents issues with spaces, slashes, and other special characters in S3 URIs
        var sanitized = Regex.Replace(fileName, @"[^a-zA-Z0-9._-]", "_");

        // Ensure the result is not empty after sanitization
        return string.IsNullOrWhiteSpace(sanitized) ? "file" : sanitized;
    }
}
