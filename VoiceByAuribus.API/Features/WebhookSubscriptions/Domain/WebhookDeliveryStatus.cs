namespace VoiceByAuribus_API.Features.WebhookSubscriptions.Domain;

/// <summary>
/// Represents the delivery status of a webhook attempt.
/// </summary>
public enum WebhookDeliveryStatus
{
    /// <summary>
    /// Webhook has not been sent yet (queued for delivery).
    /// </summary>
    Pending,

    /// <summary>
    /// Webhook is currently being processed (HTTP call in progress).
    /// Used to prevent duplicate deliveries when multiple API instances are running.
    /// </summary>
    Processing,

    /// <summary>
    /// Webhook was successfully delivered (2xx HTTP status code).
    /// </summary>
    Delivered,

    /// <summary>
    /// Webhook delivery failed (4xx/5xx status code or network error).
    /// </summary>
    Failed,

    /// <summary>
    /// Webhook delivery abandoned after exceeding maximum retry attempts.
    /// </summary>
    Abandoned
}
