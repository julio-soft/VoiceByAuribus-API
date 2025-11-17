using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VoiceByAuribus_API.Shared.Interfaces;
using VoiceByAuribus_API.Features.Voices.Application.Dtos;
using VoiceByAuribus_API.Features.Voices.Application.Mappings;
using VoiceByAuribus_API.Features.Voices.Domain;
using VoiceByAuribus_API.Shared.Infrastructure.Data;

namespace VoiceByAuribus_API.Features.Voices.Application.Services;

public class VoiceModelService(
    ApplicationDbContext context,
    IS3PresignedUrlService presignedUrlService,
    ICurrentUserService currentUserService,
    ILogger<VoiceModelService> logger) : IVoiceModelService
{
    private const double PresignedLifetimeHours = 12;

    public async Task<IReadOnlyCollection<VoiceModelResponse>> GetVoicesAsync(
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Fetching all voice models");

        var voices = await context.VoiceModels
            .AsNoTracking()
            .OrderBy(v => v.Name)
            .ToListAsync(cancellationToken);

        logger.LogInformation(
            "Retrieved {Count} voice models",
            voices.Count);

        return voices
            .Select(voice => MapVoiceModel(voice))
            .ToList();
    }

    public async Task<VoiceModelResponse?> GetVoiceAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Fetching voice model: VoiceModelId={VoiceModelId}",
            id);

        var voice = await context.VoiceModels
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

        if (voice is null)
        {
            logger.LogWarning(
                "Voice model not found: VoiceModelId={VoiceModelId}",
                id);
            return null;
        }

        logger.LogInformation(
            "Voice model retrieved: VoiceModelId={VoiceModelId}, Name={Name}",
            id, voice.Name);

        return MapVoiceModel(voice);
    }

    private VoiceModelResponse MapVoiceModel(VoiceModel voice)
    {
        var includeInternalPaths = currentUserService.IsAdmin;
        var imageUrl = CreatePresignedUrl(voice.ImageUri);
        var songUrl = CreatePresignedUrl(voice.SongUri);

        return voice.ToResponse(imageUrl, songUrl, includeInternalPaths);
    }

    private string CreatePresignedUrl(string s3Uri)
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
