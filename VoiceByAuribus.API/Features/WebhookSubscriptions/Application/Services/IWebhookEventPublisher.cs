using VoiceByAuribus_API.Features.VoiceConversions.Domain;
using VoiceByAuribus_API.Features.WebhookSubscriptions.Domain;

namespace VoiceByAuribus_API.Features.WebhookSubscriptions.Application.Services;

/// <summary>
/// Service for publishing webhook events when voice conversions complete or fail.
/// </summary>
public interface IWebhookEventPublisher
{
    /// <summary>
    /// Publishes a webhook event for a voice conversion.
    /// Creates delivery log entries for all active subscriptions listening to this event.
    /// </summary>
    /// <param name="conversion">The voice conversion that triggered the event.</param>
    /// <param name="eventType">The type of event (completed or failed).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PublishAsync(VoiceConversion conversion, WebhookEvent eventType);
}
