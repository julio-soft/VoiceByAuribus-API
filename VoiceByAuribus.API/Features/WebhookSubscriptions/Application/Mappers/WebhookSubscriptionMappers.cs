using VoiceByAuribus_API.Features.WebhookSubscriptions.Application.Dtos;
using VoiceByAuribus_API.Features.WebhookSubscriptions.Domain;

namespace VoiceByAuribus_API.Features.WebhookSubscriptions.Application.Mappers;

/// <summary>
/// Extension methods for mapping WebhookSubscription entities to DTOs.
/// </summary>
public static class WebhookSubscriptionMappers
{
    /// <summary>
    /// Maps WebhookSubscription entity to created response DTO with plain secret.
    /// This should ONLY be used when creating a subscription or regenerating a secret.
    /// </summary>
    /// <param name="subscription">The webhook subscription entity.</param>
    /// <param name="plainSecret">The plain text secret to include in the response.</param>
    /// <returns>The mapped created response DTO including the secret.</returns>
    public static CreatedWebhookSubscriptionResponseDto ToCreatedResponseDto(
        this WebhookSubscription subscription,
        string plainSecret)
    {
        return new CreatedWebhookSubscriptionResponseDto
        {
            Id = subscription.Id,
            Url = subscription.Url,
            Description = subscription.Description,
            Events = subscription.SubscribedEvents,
            IsActive = subscription.IsActive,
            LastSuccessAt = subscription.LastSuccessAt,
            LastFailureAt = subscription.LastFailureAt,
            ConsecutiveFailures = subscription.ConsecutiveFailures,
            CreatedAt = subscription.CreatedAt,
            UpdatedAt = subscription.UpdatedAt,
            Secret = plainSecret
        };
    }

    /// <summary>
    /// Maps WebhookSubscription entity to response DTO.
    /// </summary>
    /// <param name="subscription">The webhook subscription entity.</param>
    /// <returns>The mapped response DTO.</returns>
    public static WebhookSubscriptionResponseDto ToResponseDto(this WebhookSubscription subscription)
    {
        return new WebhookSubscriptionResponseDto
        {
            Id = subscription.Id,
            Url = subscription.Url,
            Description = subscription.Description,
            Events = subscription.SubscribedEvents,
            IsActive = subscription.IsActive,
            LastSuccessAt = subscription.LastSuccessAt,
            LastFailureAt = subscription.LastFailureAt,
            ConsecutiveFailures = subscription.ConsecutiveFailures,
            CreatedAt = subscription.CreatedAt,
            UpdatedAt = subscription.UpdatedAt
        };
    }

    /// <summary>
    /// Maps WebhookDeliveryLog entity to response DTO.
    /// </summary>
    /// <param name="log">The webhook delivery log entity.</param>
    /// <returns>The mapped delivery log DTO.</returns>
    public static WebhookDeliveryLogResponseDto ToDeliveryLogDto(this WebhookDeliveryLog log)
    {
        return new WebhookDeliveryLogResponseDto
        {
            Id = log.Id,
            EventType = log.EventType,
            EntityType = log.EntityType,
            EntityId = log.EntityId,
            Event = log.Event,
            Status = log.Status,
            AttemptedAt = log.AttemptedAt,
            AttemptNumber = log.AttemptNumber,
            HttpStatusCode = log.HttpStatusCode,
            ResponseBody = log.ResponseBody,
            ErrorMessage = log.ErrorMessage,
            DurationMs = log.DurationMs ?? 0,
            CreatedAt = log.CreatedAt
        };
    }
}
