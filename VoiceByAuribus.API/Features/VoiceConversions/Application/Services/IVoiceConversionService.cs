using System;
using System.Threading.Tasks;
using VoiceByAuribus_API.Features.VoiceConversions.Application.Dtos;

namespace VoiceByAuribus_API.Features.VoiceConversions.Application.Services;

/// <summary>
/// Service for managing voice conversion operations.
/// </summary>
public interface IVoiceConversionService
{
    /// <summary>
    /// Creates a new voice conversion request.
    /// </summary>
    /// <param name="dto">The conversion request data</param>
    /// <param name="userId">ID of the user creating the conversion</param>
    Task<VoiceConversionResponseDto> CreateVoiceConversionAsync(CreateVoiceConversionDto dto, Guid userId);

    /// <summary>
    /// Gets a voice conversion by ID.
    /// </summary>
    /// <param name="conversionId">ID of the conversion</param>
    /// <param name="userId">ID of the current user</param>
    Task<VoiceConversionResponseDto?> GetVoiceConversionAsync(Guid conversionId, Guid userId);

    /// <summary>
    /// Handles webhook callback from external voice conversion service.
    /// </summary>
    /// <param name="dto">Webhook callback data</param>
    Task HandleConversionResultAsync(VoiceConversionWebhookDto dto);

    /// <summary>
    /// Processes pending conversions waiting for audio preprocessing to complete.
    /// Called by background processor (Lambda).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for timeout/shutdown</param>
    /// <returns>Tuple with (processed count, skipped count)</returns>
    Task<(int Processed, int Skipped)> ProcessPendingConversionsAsync(CancellationToken cancellationToken = default);
}
