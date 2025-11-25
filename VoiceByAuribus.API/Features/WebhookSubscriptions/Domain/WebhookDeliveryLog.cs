using VoiceByAuribus_API.Shared.Domain;

namespace VoiceByAuribus_API.Features.WebhookSubscriptions.Domain;

/// <summary>
/// Represents a log entry for a webhook delivery attempt.
/// Tracks both successful and failed deliveries for observability and debugging.
/// Event-agnostic design allows supporting multiple event types (conversions, training, etc.).
/// </summary>
public class WebhookDeliveryLog : BaseAuditableEntity
{
    /// <summary>
    /// Gets or sets the ID of the webhook subscription that this delivery belongs to.
    /// </summary>
    public Guid WebhookSubscriptionId { get; set; }

    /// <summary>
    /// Navigation property to the webhook subscription.
    /// </summary>
    public WebhookSubscription WebhookSubscription { get; set; } = null!;

    /// <summary>
    /// Gets or sets the event type that triggered this webhook delivery.
    /// Examples: "conversion.completed", "conversion.failed", "training.completed", etc.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of entity that triggered this event.
    /// Examples: "voice_conversion", "voice_model_training", etc.
    /// Used for categorization and querying.
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Gets or sets the ID of the entity that triggered this webhook event.
    /// This could be a VoiceConversionId, VoiceModelTrainingId, etc.
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// Gets or sets the webhook event enum (for backwards compatibility and filtering).
    /// Maps to the old Event enum for existing subscriptions.
    /// </summary>
    public WebhookEvent Event { get; set; }

    /// <summary>
    /// Gets or sets the current delivery status.
    /// </summary>
    public WebhookDeliveryStatus Status { get; set; } = WebhookDeliveryStatus.Pending;

    /// <summary>
    /// Gets or sets the JSON payload that was/will be sent to the webhook URL.
    /// </summary>
    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when this delivery attempt was made.
    /// </summary>
    public DateTime? AttemptedAt { get; set; }

    /// <summary>
    /// Gets or sets the attempt number (1-based). Used for retry tracking.
    /// </summary>
    public int AttemptNumber { get; set; } = 1;

    /// <summary>
    /// Gets or sets the HTTP status code received from the webhook endpoint.
    /// </summary>
    public int? HttpStatusCode { get; set; }

    /// <summary>
    /// Gets or sets the response body received from the webhook endpoint.
    /// Truncated to 2000 characters for storage efficiency.
    /// </summary>
    public string? ResponseBody { get; set; }

    /// <summary>
    /// Gets or sets the error message if the delivery failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the duration of the HTTP request in milliseconds.
    /// </summary>
    public int? DurationMs { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the next retry should be attempted.
    /// Null if no retry is scheduled or delivery was successful.
    /// </summary>
    public DateTime? NextRetryAt { get; set; }

    /// <summary>
    /// Gets or sets the row version for optimistic concurrency control.
    /// Automatically updated by the database on each modification.
    /// Prevents race conditions when multiple API instances process webhooks.
    /// </summary>
    public byte[] RowVersion { get; set; } = [];
}
