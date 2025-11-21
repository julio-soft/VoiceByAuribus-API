using System;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using VoiceByAuribus_API.Features.Auth.Presentation;
using VoiceByAuribus_API.Features.VoiceConversions.Application.Dtos;
using VoiceByAuribus_API.Features.VoiceConversions.Application.Services;
using VoiceByAuribus_API.Shared.Domain;
using VoiceByAuribus_API.Shared.Infrastructure.Controllers;
using VoiceByAuribus_API.Shared.Infrastructure.Filters;
using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Features.VoiceConversions.Presentation.Controllers;

/// <summary>
/// Controller for managing voice conversion operations.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/voice-conversions")]
public class VoiceConversionsController : BaseController
{
    private readonly IVoiceConversionService _voiceConversionService;
    private readonly ILogger<VoiceConversionsController> _logger;

    public VoiceConversionsController(
        ICurrentUserService currentUserService,
        IVoiceConversionService voiceConversionService,
        ILogger<VoiceConversionsController> logger)
        : base(currentUserService)
    {
        _voiceConversionService = voiceConversionService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new voice conversion request.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Base)]
    [ProducesResponseType(typeof(ApiResponse<VoiceConversionResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateVoiceConversionAsync([FromBody] CreateVoiceConversionDto dto)
    {
        try
        {
            var userId = GetUserId();
            _logger.LogInformation(
                "[API] POST /voice-conversions - UserId={UserId}, AudioFileId={AudioFileId}, VoiceModelId={VoiceModelId}",
                userId, dto.AudioFileId, dto.VoiceModelId);

            var response = await _voiceConversionService.CreateVoiceConversionAsync(dto, userId);
            return CreatedAtAction(
                nameof(GetVoiceConversionAsync),
                new { id = response.Id },
                ApiResponse<VoiceConversionResponseDto>.SuccessResponse(response));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "[API] Failed to create voice conversion: {Message}",
                ex.Message);
            return Error<object>(ex.Message);
        }
    }

    /// <summary>
    /// Gets a voice conversion by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Base)]
    [ProducesResponseType(typeof(ApiResponse<VoiceConversionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetVoiceConversionAsync(Guid id)
    {
        var userId = GetUserId();
        _logger.LogInformation(
            "[API] GET /voice-conversions/{Id} - UserId={UserId}",
            id, userId);

        var conversion = await _voiceConversionService.GetVoiceConversionAsync(id, userId);
        if (conversion is null)
        {
            return NotFound(ApiResponse<VoiceConversionResponseDto>.ErrorResponse("Voice conversion not found"));
        }

        return Success(conversion);
    }
}
