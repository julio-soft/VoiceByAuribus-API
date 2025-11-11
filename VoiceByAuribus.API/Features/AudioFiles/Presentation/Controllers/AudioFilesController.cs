using System;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VoiceByAuribus_API.Features.AudioFiles.Application.Dtos;
using VoiceByAuribus_API.Features.AudioFiles.Application.Services;
using VoiceByAuribus_API.Features.Auth.Presentation;
using VoiceByAuribus_API.Shared.Domain;
using VoiceByAuribus_API.Shared.Infrastructure.Controllers;
using VoiceByAuribus_API.Shared.Infrastructure.Filters;
using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Features.AudioFiles.Presentation.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/audio-files")]
public class AudioFilesController : BaseController
{
    private readonly IAudioFileService _audioFileService;
    private readonly IAudioPreprocessingService _preprocessingService;

    public AudioFilesController(
        ICurrentUserService currentUserService,
        IAudioFileService audioFileService,
        IAudioPreprocessingService preprocessingService)
        : base(currentUserService)
    {
        _audioFileService = audioFileService;
        _preprocessingService = preprocessingService;
    }

    /// <summary>
    /// Creates a new audio file record and returns pre-signed upload URL
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Base)]
    [ProducesResponseType(typeof(ApiResponse<AudioFileCreatedResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateAudioFileAsync([FromBody] CreateAudioFileDto dto)
    {
        var userId = GetUserId();
        var response = await _audioFileService.CreateAudioFileAsync(dto, userId);
        return CreatedAtAction(nameof(GetAudioFileAsync), new { id = response.Id }, ApiResponse<AudioFileCreatedResponseDto>.SuccessResponse(response));
    }

    /// <summary>
    /// Regenerates upload URL for a file that hasn't been uploaded yet
    /// </summary>
    [HttpPost("{id:guid}/regenerate-upload-url")]
    [Authorize(Policy = AuthorizationPolicies.Base)]
    [ProducesResponseType(typeof(ApiResponse<RegenerateUploadUrlResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RegenerateUploadUrlAsync(Guid id)
    {
        try
        {
            var userId = GetUserId();
            var response = await _audioFileService.RegenerateUploadUrlAsync(id, userId);
            return Success(response);
        }
        catch (InvalidOperationException ex)
        {
            return Error<object>(ex.Message);
        }
    }

    /// <summary>
    /// Gets an audio file by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Base)]
    [ProducesResponseType(typeof(ApiResponse<AudioFileResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAudioFileAsync(Guid id)
    {
        var userId = GetUserId();
        var isAdmin = CurrentUserService.IsAdmin;
        var response = await _audioFileService.GetAudioFileByIdAsync(id, userId, isAdmin);

        if (response is null)
        {
            return NotFound(ApiResponse<object>.ErrorResponse("Audio file not found"));
        }

        return Success(response);
    }

    /// <summary>
    /// Gets paginated list of user's audio files
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.Base)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAudioFilesAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var userId = GetUserId();
        var (items, totalCount) = await _audioFileService.GetUserAudioFilesAsync(userId, page, pageSize);

        var response = new
        {
            Items = items,
            Pagination = new
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            }
        };

        return Success(response);
    }

    /// <summary>
    /// Soft deletes an audio file
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Base)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteAudioFileAsync(Guid id)
    {
        var userId = GetUserId();
        var deleted = await _audioFileService.SoftDeleteAsync(id, userId);

        if (!deleted)
        {
            return NotFound(ApiResponse<object>.ErrorResponse("Audio file not found"));
        }

        return NoContent();
    }

    /// <summary>
    /// Webhook endpoint for S3 upload notifications (internal use only)
    /// </summary>
    [HttpPost("webhook/upload-notification")]
    [WebhookAuthentication]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UploadNotificationAsync([FromBody] UploadNotificationDto dto)
    {
        try
        {
            await _audioFileService.HandleUploadNotificationAsync(dto.S3Uri);
            return Success(new { Message = "Upload notification processed successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return Error<object>(ex.Message);
        }
    }

    /// <summary>
    /// Webhook endpoint for preprocessing results (internal use only)
    /// </summary>
    [HttpPost("webhook/preprocessing-result")]
    [WebhookAuthentication]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> PreprocessingResultAsync([FromBody] PreprocessingResultDto dto)
    {
        try
        {
            await _preprocessingService.HandlePreprocessingResultAsync(dto);
            return Success(new { Message = "Preprocessing result processed successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return Error<object>(ex.Message);
        }
    }
}
