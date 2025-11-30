using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VoiceByAuribus_API.Features.Auth.Presentation;
using VoiceByAuribus_API.Features.WebhookSubscriptions.Application.Dtos;
using VoiceByAuribus_API.Features.WebhookSubscriptions.Application.Services;
using VoiceByAuribus_API.Shared.Domain;
using VoiceByAuribus_API.Shared.Infrastructure.Controllers;
using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Features.WebhookSubscriptions.Presentation.Controllers;

/// <summary>
/// Controller for managing webhook subscriptions.
/// Allows external clients to configure webhook notifications for voice conversion events.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/webhooks/subscriptions")]
[Authorize(Policy = AuthorizationPolicies.Base)]
public class WebhookSubscriptionsController(
    IWebhookSubscriptionService subscriptionService,
    ICurrentUserService currentUserService) : BaseController(currentUserService)
{
    /// <summary>
    /// Creates a new webhook subscription.
    /// The secret will be hashed and stored securely.
    /// IMPORTANT: The plain text secret is shown ONLY in this response.
    /// </summary>
    /// <param name="dto">The webhook subscription details.</param>
    /// <returns>The created subscription with ID.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<WebhookSubscriptionResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateSubscription([FromBody] CreateWebhookSubscriptionDto dto)
    {
        try
        {
            var userId = GetUserId();
            var subscription = await subscriptionService.CreateSubscriptionAsync(dto, userId);

            return Created(
                $"/api/v1/webhooks/subscriptions/{subscription.Id}",
                ApiResponse<WebhookSubscriptionResponseDto>.SuccessResponse(
                    subscription,
                    "Webhook subscription created successfully. " +
                    "Your secret has been securely hashed and stored. " +
                    "Make sure to save your secret as it cannot be retrieved later."
                )
            );
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse(ex.Message));
        }
    }

    /// <summary>
    /// Gets all webhook subscriptions for the current user.
    /// </summary>
    /// <returns>List of webhook subscriptions.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<WebhookSubscriptionResponseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubscriptions()
    {
        var userId = GetUserId();
        var subscriptions = await subscriptionService.GetUserSubscriptionsAsync(userId);
        return Success(subscriptions);
    }

    /// <summary>
    /// Gets a specific webhook subscription by ID.
    /// </summary>
    /// <param name="id">The subscription ID.</param>
    /// <returns>The webhook subscription details.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<WebhookSubscriptionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubscription([FromRoute] Guid id)
    {
        var userId = GetUserId();
        var subscription = await subscriptionService.GetSubscriptionByIdAsync(id, userId);

        if (subscription is null)
        {
            return NotFound(ApiResponse<object>.ErrorResponse("Webhook subscription not found"));
        }

        return Success(subscription);
    }

    /// <summary>
    /// Updates a webhook subscription.
    /// Can update URL, description, subscribed events, or active status.
    /// </summary>
    /// <param name="id">The subscription ID.</param>
    /// <param name="dto">The fields to update.</param>
    /// <returns>The updated subscription details.</returns>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<WebhookSubscriptionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSubscription(
        [FromRoute] Guid id,
        [FromBody] UpdateWebhookSubscriptionDto dto)
    {
        var userId = GetUserId();
        var subscription = await subscriptionService.UpdateSubscriptionAsync(id, dto, userId);

        if (subscription is null)
        {
            return NotFound(ApiResponse<object>.ErrorResponse("Webhook subscription not found"));
        }

        return Success(subscription, "Webhook subscription updated successfully");
    }

    /// <summary>
    /// Deletes a webhook subscription (soft delete).
    /// All associated delivery logs will also be marked as deleted.
    /// </summary>
    /// <param name="id">The subscription ID.</param>
    /// <returns>Success message.</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSubscription([FromRoute] Guid id)
    {
        var userId = GetUserId();
        var deleted = await subscriptionService.DeleteSubscriptionAsync(id, userId);

        if (!deleted)
        {
            return NotFound(ApiResponse<object>.ErrorResponse("Webhook subscription not found"));
        }

        return Success(new
        {
            Message = "Webhook subscription deleted successfully",
            SubscriptionId = id
        });
    }

    /// <summary>
    /// Regenerates the secret for a webhook subscription.
    /// The new secret will be returned in plain text - this is the ONLY time it will be shown.
    /// The old secret will be immediately invalidated.
    /// </summary>
    /// <param name="id">The subscription ID.</param>
    /// <returns>The new secret and instructions.</returns>
    [HttpPost("{id:guid}/regenerate-secret")]
    [ProducesResponseType(typeof(ApiResponse<RegenerateSecretResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegenerateSecret([FromRoute] Guid id)
    {
        try
        {
            var userId = GetUserId();
            var result = await subscriptionService.RegenerateSecretAsync(id, userId);

            return Success(result, "Secret regenerated successfully");
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<object>.ErrorResponse(ex.Message));
        }
    }

    /// <summary>
    /// Sends a test webhook to verify the endpoint is reachable.
    /// Note: Due to secret hashing, this requires the plain text secret to be provided separately,
    /// or you can trigger a real conversion to test the webhook flow.
    /// </summary>
    /// <param name="id">The subscription ID.</param>
    /// <returns>The test result.</returns>
    [HttpPost("{id:guid}/test")]
    [ProducesResponseType(typeof(ApiResponse<WebhookTestResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TestWebhook([FromRoute] Guid id)
    {
        try
        {
            var userId = GetUserId();
            var result = await subscriptionService.TestWebhookAsync(id, userId);

            return Success(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<object>.ErrorResponse(ex.Message));
        }
    }

    /// <summary>
    /// Gets delivery logs for a webhook subscription.
    /// Shows the history of webhook delivery attempts with status, response codes, and errors.
    /// </summary>
    /// <param name="id">The subscription ID.</param>
    /// <param name="limit">Maximum number of logs to return (default 100, max 500).</param>
    /// <returns>List of delivery logs.</returns>
    [HttpGet("{id:guid}/deliveries")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<WebhookDeliveryLogResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDeliveryLogs(
        [FromRoute] Guid id,
        [FromQuery] int limit = 100)
    {
        var userId = GetUserId();
        var logs = await subscriptionService.GetDeliveryLogsAsync(id, userId, limit);

        if (!logs.Any())
        {
            // Could be either no logs or subscription not found - check which
            var subscription = await subscriptionService.GetSubscriptionByIdAsync(id, userId);
            if (subscription is null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("Webhook subscription not found"));
            }
        }

        return Success(logs);
    }
}
