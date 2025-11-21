using System;
using VoiceByAuribus_API.Features.VoiceConversions.Application.Dtos;
using VoiceByAuribus_API.Features.VoiceConversions.Application.Helpers;
using VoiceByAuribus_API.Features.VoiceConversions.Domain;
using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Features.VoiceConversions.Application.Mappers;

/// <summary>Mapper for VoiceConversion entity to DTOs.</summary>
public static class VoiceConversionMapper
{
    private const double PresignedLifetimeHours = 12;

    /// <summary>Maps VoiceConversion entity to VoiceConversionResponseDto.</summary>
    /// <param name="conversion">The voice conversion entity to map</param>
    /// <param name="presignedUrlService">Service for generating pre-signed URLs</param>
    /// <param name="isAdmin">Whether the current user is an admin</param>
    public static VoiceConversionResponseDto MapToResponseDto(
        VoiceConversion conversion,
        IS3PresignedUrlService presignedUrlService,
        bool isAdmin)
    {
        var dto = new VoiceConversionResponseDto
        {
            Id = conversion.Id,
            AudioFileId = conversion.AudioFileId,
            AudioFileName = conversion.AudioFile.FileName,
            VoiceModelId = conversion.VoiceModelId,
            VoiceModelName = conversion.VoiceModel.Name,
            PitchShift = PitchShiftHelper.ToPitchShiftString(conversion.Transposition),
            UsePreview = conversion.UsePreview,
            Status = conversion.Status.ToString(),
            CreatedAt = conversion.CreatedAt,
            QueuedAt = conversion.QueuedAt,
            ProcessingStartedAt = conversion.ProcessingStartedAt,
            CompletedAt = conversion.CompletedAt,
            ErrorMessage = conversion.ErrorMessage
        };

        // Generate pre-signed URL for completed conversions
        if (conversion.Status == ConversionStatus.Completed && !string.IsNullOrEmpty(conversion.OutputS3Uri))
        {
            dto.OutputUrl = CreatePresignedUrl(conversion.OutputS3Uri, presignedUrlService);
        }

        // Admin-only data
        if (isAdmin)
        {
            dto.OutputS3Uri = conversion.OutputS3Uri;
            dto.RetryCount = conversion.RetryCount;
        }

        return dto;
    }

    private static string CreatePresignedUrl(string s3Uri, IS3PresignedUrlService presignedUrlService)
    {
        var (bucket, key) = ParseS3Uri(s3Uri);
        return presignedUrlService.CreatePublicUrl(bucket, key, TimeSpan.FromHours(PresignedLifetimeHours));
    }

    private static (string Bucket, string Key) ParseS3Uri(string uri)
    {
        const string prefix = "s3://";
        if (!uri.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Expected S3 URI with '{prefix}' prefix.");
        }

        var path = uri[prefix.Length..];
        var separatorIndex = path.IndexOf('/', StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex == path.Length - 1)
        {
            throw new InvalidOperationException("S3 URI must include bucket and key, e.g., s3://bucket/key");
        }

        var bucket = path[..separatorIndex];
        var key = path[(separatorIndex + 1)..];
        return (bucket, key);
    }
}
