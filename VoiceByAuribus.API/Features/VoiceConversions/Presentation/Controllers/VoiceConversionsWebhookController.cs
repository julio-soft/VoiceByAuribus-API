using System;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using VoiceByAuribus_API.Features.VoiceConversions.Application.Dtos;
using VoiceByAuribus_API.Features.VoiceConversions.Application.Services;
using VoiceByAuribus_API.Shared.Domain;
using VoiceByAuribus_API.Shared.Infrastructure.Filters;

namespace VoiceByAuribus_API.Features.VoiceConversions.Presentation.Controllers;

/// <summary>
/// Controller for webhook endpoints related to voice conversions.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/voice-conversions")]
public class VoiceConversionsWebhookController : ControllerBase
{
    private readonly IVoiceConversionService _voiceConversionService;
    private readonly ILogger<VoiceConversionsWebhookController> _logger;

    public VoiceConversionsWebhookController(
        IVoiceConversionService voiceConversionService,
        ILogger<VoiceConversionsWebhookController> logger)
    {
        _voiceConversionService = voiceConversionService;
        _logger = logger;
    }

    /// <summary>
    /// Webhook endpoint for voice conversion results from external service (internal use only).
    /// </summary>
    [HttpPost("webhooks/conversion-result")]
    [WebhookAuthentication]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ConversionResultAsync([FromBody] VoiceConversionWebhookDto dto)
    {
        _logger.LogInformation(
            "[WEBHOOK] POST /voice-conversions/webhooks/conversion-result - RequestId={RequestId}, Status={Status}, FinishedAtUtc={FinishedAtUtc}",
            dto.RequestId, dto.Status, dto.FinishedAtUtc);

        try
        {
            await _voiceConversionService.HandleConversionResultAsync(dto);
            return Ok(ApiResponse<object>.SuccessResponse(
                new { Message = "Conversion result processed successfully" }));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Conversion result webhook failed: {Message}", ex.Message);
            return BadRequest(ApiResponse<object>.ErrorResponse(ex.Message));
        }
    }
}
