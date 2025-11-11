using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VoiceByAuribus_API.Features.Auth.Application.Dtos;
using VoiceByAuribus_API.Features.Auth.Application.Services;
using VoiceByAuribus_API.Shared.Domain;
using VoiceByAuribus_API.Shared.Infrastructure.Controllers;
using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Features.Auth.Presentation.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public class AuthController : BaseController
{
    private readonly IAuthReadService _authReadService;

    public AuthController(ICurrentUserService currentUserService, IAuthReadService authReadService) 
        : base(currentUserService)
    {
        _authReadService = authReadService;
    }

    [HttpGet("current-user")]
    [Authorize(Policy = AuthorizationPolicies.Base)]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCurrentUserAsync()
    {
        var response = await _authReadService.GetCurrentUserAsync();
        return Success(response);
    }

    [HttpGet("status")]
    [Authorize(Policy = AuthorizationPolicies.Base)]
    [ProducesResponseType(typeof(ApiResponse<AuthStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStatusAsync()
    {
        var response = await _authReadService.GetStatusAsync();
        return Success(response);
    }
}
