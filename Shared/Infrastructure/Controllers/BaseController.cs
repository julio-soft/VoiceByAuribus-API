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
    /// Gets the current authenticated user's Cognito ID
    /// </summary>
    protected Guid GetUserId()
    {
        var userId = CurrentUserService.UserId;
        if (userId is null)
        {
            throw new UnauthorizedAccessException("User is not authenticated");
        }
        return userId.Value;
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
