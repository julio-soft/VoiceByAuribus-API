using VoiceByAuribus_API.Features.WebhookSubscriptions.Application.Dtos;

namespace VoiceByAuribus_API.Features.WebhookSubscriptions.Application.Services;

/// <summary>
/// Service for managing webhook subscriptions.
/// </summary>
public interface IWebhookSubscriptionService
{
    /// <summary>
    /// Creates a new webhook subscription with an auto-generated secret.
    /// The secret is returned in the response and will ONLY be shown this one time.
    /// </summary>
    /// <param name="dto">The creation DTO.</param>
    /// <param name="userId">The ID of the user creating the subscription.</param>
    /// <returns>The created subscription details including the generated secret.</returns>
    Task<CreatedWebhookSubscriptionResponseDto> CreateSubscriptionAsync(
        CreateWebhookSubscriptionDto dto, string userId);

    /// <summary>
    /// Gets all webhook subscriptions for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>List of subscriptions.</returns>
    Task<IEnumerable<WebhookSubscriptionResponseDto>> GetUserSubscriptionsAsync(string userId);

    /// <summary>
    /// Gets a specific webhook subscription by ID.
    /// </summary>
    /// <param name="id">The subscription ID.</param>
    /// <param name="userId">The user ID (for ownership verification).</param>
    /// <returns>The subscription details, or null if not found.</returns>
    Task<WebhookSubscriptionResponseDto?> GetSubscriptionByIdAsync(Guid id, string userId);

    /// <summary>
    /// Updates a webhook subscription.
    /// </summary>
    /// <param name="id">The subscription ID.</param>
    /// <param name="dto">The update DTO.</param>
    /// <param name="userId">The user ID (for ownership verification).</param>
    /// <returns>The updated subscription details, or null if not found.</returns>
    Task<WebhookSubscriptionResponseDto?> UpdateSubscriptionAsync(
        Guid id, UpdateWebhookSubscriptionDto dto, string userId);

    /// <summary>
    /// Deletes a webhook subscription (soft delete).
    /// </summary>
    /// <param name="id">The subscription ID.</param>
    /// <param name="userId">The user ID (for ownership verification).</param>
    /// <returns>True if deleted, false if not found.</returns>
    Task<bool> DeleteSubscriptionAsync(Guid id, string userId);

    /// <summary>
    /// Regenerates the secret for a webhook subscription.
    /// Returns the new plain text secret - this is the ONLY time it will be shown.
    /// </summary>
    /// <param name="id">The subscription ID.</param>
    /// <param name="userId">The user ID (for ownership verification).</param>
    /// <returns>The regeneration result with the new secret.</returns>
    Task<RegenerateSecretResponseDto> RegenerateSecretAsync(Guid id, string userId);

    /// <summary>
    /// Sends a test webhook to verify the endpoint is reachable.
    /// </summary>
    /// <param name="id">The subscription ID.</param>
    /// <param name="userId">The user ID (for ownership verification).</param>
    /// <returns>The test result.</returns>
    Task<WebhookTestResultDto> TestWebhookAsync(Guid id, string userId);

    /// <summary>
    /// Gets delivery logs for a webhook subscription.
    /// </summary>
    /// <param name="subscriptionId">The subscription ID.</param>
    /// <param name="userId">The user ID (for ownership verification).</param>
    /// <param name="limit">Maximum number of logs to return (default 100).</param>
    /// <returns>List of delivery logs.</returns>
    Task<IEnumerable<WebhookDeliveryLogResponseDto>> GetDeliveryLogsAsync(
        Guid subscriptionId, string userId, int limit = 100);
}
