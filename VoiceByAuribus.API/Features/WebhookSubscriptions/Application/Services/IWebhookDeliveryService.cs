using VoiceByAuribus_API.Features.WebhookSubscriptions.Domain;

namespace VoiceByAuribus_API.Features.WebhookSubscriptions.Application.Services;

/// <summary>
/// Service for delivering webhooks to external client endpoints.
/// </summary>
public interface IWebhookDeliveryService
{
    /// <summary>
    /// Attempts to deliver a webhook to the specified URL.
    /// </summary>
    /// <param name="deliveryLog">The delivery log to process.</param>
    /// <param name="secret">The plain text secret for HMAC signing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated delivery log with response details.</returns>
    Task<WebhookDeliveryLog> DeliverWebhookAsync(
        WebhookDeliveryLog deliveryLog,
        string secret,
        CancellationToken cancellationToken = default);
}
