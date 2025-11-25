using VoiceByAuribus_API.Shared.Domain;
using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Features.WebhookSubscriptions.Domain;

/// <summary>
/// Represents a webhook subscription configured by a user to receive notifications about events.
/// </summary>
public class WebhookSubscription : BaseAuditableEntity, IHasUserId
{
    /// <summary>
    /// Gets or sets the ID of the user who owns this webhook subscription.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Gets or sets the HTTPS URL where webhook notifications will be sent.
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// Gets or sets the encrypted secret used for HMAC-SHA256 signature verification.
    /// The secret is encrypted using AES-256-GCM with a master key from AWS Secrets Manager.
    /// Format: {nonce}:{ciphertext}:{tag} (all base64 encoded)
    /// </summary>
    public required string EncryptedSecret { get; set; }

    /// <summary>
    /// Gets or sets an optional description for this webhook subscription.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the array of events this subscription is listening to.
    /// </summary>
    public WebhookEvent[] SubscribedEvents { get; set; } = [
        WebhookEvent.ConversionCompleted,
        WebhookEvent.ConversionFailed
    ];

    /// <summary>
    /// Gets or sets a value indicating whether this subscription is active.
    /// Inactive subscriptions will not receive webhook notifications.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the timestamp of the last successful webhook delivery.
    /// </summary>
    public DateTime? LastSuccessAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last failed webhook delivery.
    /// </summary>
    public DateTime? LastFailureAt { get; set; }

    /// <summary>
    /// Gets or sets the count of consecutive delivery failures.
    /// Resets to 0 on successful delivery.
    /// </summary>
    public int ConsecutiveFailures { get; set; } = 0;

    /// <summary>
    /// Gets or sets a value indicating whether to automatically disable this subscription
    /// after reaching the maximum number of consecutive failures.
    /// </summary>
    public bool AutoDisableOnFailure { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of consecutive failures before auto-disabling.
    /// Default is 10.
    /// </summary>
    public int MaxConsecutiveFailures { get; set; } = 10;

    /// <summary>
    /// Navigation property for webhook delivery logs.
    /// </summary>
    public ICollection<WebhookDeliveryLog> DeliveryLogs { get; set; } = new List<WebhookDeliveryLog>();
}
