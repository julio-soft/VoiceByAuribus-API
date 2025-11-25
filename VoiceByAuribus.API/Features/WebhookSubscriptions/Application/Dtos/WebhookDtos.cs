using VoiceByAuribus_API.Features.WebhookSubscriptions.Domain;

namespace VoiceByAuribus_API.Features.WebhookSubscriptions.Application.Dtos;

/// <summary>
/// DTO for updating an existing webhook subscription.
/// </summary>
public class UpdateWebhookSubscriptionDto
{
    /// <summary>
    /// Gets or sets the new URL (optional).
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Gets or sets the new description (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the new subscribed events (optional).
    /// </summary>
    public WebhookEvent[]? Events { get; set; }

    /// <summary>
    /// Gets or sets whether the subscription is active (optional).
    /// </summary>
    public bool? IsActive { get; set; }
}

/// <summary>
/// Response DTO for webhook subscription creation.
/// Includes the generated secret which will ONLY be shown this one time.
/// </summary>
public class CreatedWebhookSubscriptionResponseDto : WebhookSubscriptionResponseDto
{
    /// <summary>
    /// Gets or sets the auto-generated secret for HMAC-SHA256 signature verification.
    /// IMPORTANT: This is the ONLY time the plain text secret will be displayed.
    /// Save it securely - it cannot be retrieved later.
    /// </summary>
    public required string Secret { get; set; }
}

/// <summary>
/// Response DTO for webhook subscription details.
/// </summary>
public class WebhookSubscriptionResponseDto
{
    /// <summary>
    /// Gets or sets the subscription ID.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the webhook URL.
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the subscribed events.
    /// </summary>
    public WebhookEvent[] Events { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the subscription is active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last successful delivery.
    /// </summary>
    public DateTime? LastSuccessAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last failed delivery.
    /// </summary>
    public DateTime? LastFailureAt { get; set; }

    /// <summary>
    /// Gets or sets the count of consecutive failures.
    /// </summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Response DTO for secret regeneration.
/// </summary>
public class RegenerateSecretResponseDto
{
    /// <summary>
    /// Gets or sets the new plain text secret.
    /// This is the ONLY time it will be shown.
    /// </summary>
    public required string NewSecret { get; set; }

    /// <summary>
    /// Gets or sets an informational message.
    /// </summary>
    public required string Message { get; set; }
}

/// <summary>
/// Response DTO for webhook test results.
/// </summary>
public class WebhookTestResultDto
{
    /// <summary>
    /// Gets or sets the confirmation message.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Gets or sets the webhook URL that will receive the test.
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// Gets or sets the test payload that was sent (for reference).
    /// </summary>
    public object? TestPayload { get; set; }
}

/// <summary>
/// Response DTO for webhook delivery log details.
/// </summary>
public class WebhookDeliveryLogResponseDto
{
    /// <summary>
    /// Gets or sets the delivery log ID.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the event type string (e.g., "conversion.completed", "training.completed").
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the entity type (e.g., "voice_conversion", "voice_model_training").
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Gets or sets the related entity ID (could be voice conversion ID, training ID, etc.).
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// Gets or sets the event enum (for backwards compatibility).
    /// </summary>
    public WebhookEvent Event { get; set; }

    /// <summary>
    /// Gets or sets the delivery status.
    /// </summary>
    public WebhookDeliveryStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when the delivery was attempted.
    /// </summary>
    public DateTime? AttemptedAt { get; set; }

    /// <summary>
    /// Gets or sets the attempt number.
    /// </summary>
    public int AttemptNumber { get; set; }

    /// <summary>
    /// Gets or sets the HTTP status code received.
    /// </summary>
    public int? HttpStatusCode { get; set; }

    /// <summary>
    /// Gets or sets the response body from the webhook endpoint.
    /// </summary>
    public string? ResponseBody { get; set; }

    /// <summary>
    /// Gets or sets the error message if delivery failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the request duration in milliseconds.
    /// </summary>
    public int? DurationMs { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
