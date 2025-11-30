using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using VoiceByAuribus_API.Shared.Domain;
using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Shared.Infrastructure.Controllers;

/// <summary>
/// Base controller with common functionality for authenticated endpoints
/// </summary>
[ApiController]
public abstract class BaseController : ControllerBase
{
    protected ICurrentUserService CurrentUserService { get; }

    protected BaseController(ICurrentUserService currentUserService)
    {
        CurrentUserService = currentUserService;
    }

    /// <summary>
    /// Gets the current authenticated user's ID from the JWT token.
    /// For M2M tokens, this is the Cognito client_id.
    /// For user tokens, this is the user's sub claim.
    /// </summary>
    protected string GetUserId()
    {
        var userId = CurrentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException("User is not authenticated");
        }
        return userId;
    }

    /// <summary>
    /// Returns a success response
    /// </summary>
    protected IActionResult Success<T>(T data, string? message = null)
    {
        return Ok(ApiResponse<T>.SuccessResponse(data, message));
    }

    /// <summary>
    /// Returns an error response
    /// </summary>
    protected IActionResult Error<T>(string message, List<string>? errors = null)
    {
        return BadRequest(ApiResponse<T>.ErrorResponse(message, errors));
    }
}
