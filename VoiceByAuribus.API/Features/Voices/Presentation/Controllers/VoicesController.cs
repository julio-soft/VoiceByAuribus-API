using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VoiceByAuribus_API.Features.Voices.Application.Dtos;
using VoiceByAuribus_API.Features.Voices.Application.Services;
using VoiceByAuribus_API.Features.Auth.Presentation;
using VoiceByAuribus_API.Shared.Domain;
using VoiceByAuribus_API.Shared.Infrastructure.Controllers;
using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Features.Voices.Presentation.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/voices")]
public class VoicesController : BaseController
{
    private readonly IVoiceModelService _voiceModelService;

    public VoicesController(ICurrentUserService currentUserService, IVoiceModelService voiceModelService) 
        : base(currentUserService)
    {
        _voiceModelService = voiceModelService;
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.Base)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<VoiceModelResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetVoicesAsync(CancellationToken cancellationToken)
    {
        var response = await _voiceModelService.GetVoicesAsync(cancellationToken);
        return Success(response);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Base)]
    [ProducesResponseType(typeof(ApiResponse<VoiceModelResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<VoiceModelResponse>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetVoiceAsync(Guid id, CancellationToken cancellationToken)
    {
        var response = await _voiceModelService.GetVoiceAsync(id, cancellationToken);
        if (response is null)
        {
            return Error<VoiceModelResponse>("Voice model not found");
        }

        return Success(response);
    }
}
