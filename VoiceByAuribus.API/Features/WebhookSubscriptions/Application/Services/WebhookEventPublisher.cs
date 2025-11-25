using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VoiceByAuribus_API.Features.VoiceConversions.Application.Helpers;
using VoiceByAuribus_API.Features.VoiceConversions.Domain;
using VoiceByAuribus_API.Features.WebhookSubscriptions.Domain;
using VoiceByAuribus_API.Shared.Domain;
using VoiceByAuribus_API.Shared.Infrastructure.Data;
using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Features.WebhookSubscriptions.Application.Services;

/// <summary>
/// Service for publishing webhook events to subscribed clients.
/// </summary>
public class WebhookEventPublisher(
    ApplicationDbContext context,
    ILogger<WebhookEventPublisher> logger) : IWebhookEventPublisher
{
    /// <inheritdoc />
    public async Task PublishAsync(VoiceConversion conversion, WebhookEvent eventType)
    {
        // Find all active subscriptions for this user listening to this event
        var subscriptions = await context.WebhookSubscriptions
            .Where(s => s.UserId == conversion.UserId &&
                       s.IsActive &&
                       s.SubscribedEvents.Contains(eventType))
            .ToListAsync();

        if (!subscriptions.Any())
        {
            logger.LogDebug(
                "[WEBHOOK] No active subscriptions found for event - Event={Event}, UserId={UserId}, ConversionId={ConversionId}",
                eventType, conversion.UserId, conversion.Id);
            return;
        }

        logger.LogInformation(
            "[WEBHOOK] Publishing event to {Count} subscription(s) - Event={Event}, ConversionId={ConversionId}",
            subscriptions.Count, eventType, conversion.Id);

        foreach (var subscription in subscriptions)
        {
            try
            {
                // Build webhook payload
                var payload = BuildPayload(conversion, eventType);
                var payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    WriteIndented = false
                });

                // Create delivery log entry (will be processed by background service)
                var eventName = eventType == WebhookEvent.ConversionCompleted
                    ? WebhookEventTypes.ConversionCompleted
                    : WebhookEventTypes.ConversionFailed;

                var deliveryLog = new WebhookDeliveryLog
                {
                    WebhookSubscriptionId = subscription.Id,
                    EventType = eventName,
                    EntityType = WebhookEntityTypes.VoiceConversion,
                    EntityId = conversion.Id,
                    Event = eventType, // Keep enum for filtering
                    Status = WebhookDeliveryStatus.Pending,
                    PayloadJson = payloadJson,
                    AttemptNumber = 1,
                    NextRetryAt = null // First attempt should be immediate (no delay)
                };

                context.WebhookDeliveryLogs.Add(deliveryLog);

                logger.LogDebug(
                    "[WEBHOOK] Created delivery log - LogId={LogId}, SubscriptionId={SubscriptionId}",
                    deliveryLog.Id, subscription.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "[WEBHOOK] Failed to create delivery log - SubscriptionId={SubscriptionId}, ConversionId={ConversionId}",
                    subscription.Id, conversion.Id);
            }
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Builds the webhook payload for a voice conversion event.
    /// Uses PitchShift abstraction (not internal Transposition enum).
    /// Does NOT include output_url - client should fetch via API for fresh URLs.
    /// </summary>
    private object BuildPayload(VoiceConversion conversion, WebhookEvent eventType)
    {
        var eventName = eventType == WebhookEvent.ConversionCompleted
            ? WebhookEventTypes.ConversionCompleted
            : WebhookEventTypes.ConversionFailed;

        // Base conversion data
        var conversionData = new Dictionary<string, object?>
        {
            ["id"] = conversion.Id,
            ["status"] = conversion.Status.ToString().ToLowerInvariant(),
            ["audio_file_id"] = conversion.AudioFileId,
            ["voice_model_id"] = conversion.VoiceModelId,
            ["pitch_shift"] = PitchShiftHelper.ToPitchShiftString(conversion.Transposition), // ✅ Use PitchShift abstraction
            ["use_preview"] = conversion.UsePreview,
            ["queued_at"] = conversion.QueuedAt,
            ["processing_started_at"] = conversion.ProcessingStartedAt,
            ["completed_at"] = conversion.CompletedAt
        };

        // Add event-specific data
        if (eventType == WebhookEvent.ConversionCompleted)
        {
            // ✅ Calculate processing duration
            if (conversion.ProcessingStartedAt.HasValue && conversion.CompletedAt.HasValue)
            {
                var duration = (conversion.CompletedAt.Value - conversion.ProcessingStartedAt.Value).TotalSeconds;
                conversionData["processing_duration_seconds"] = (int)duration;
            }

            // ✅ NOTE: output_url NOT included - client should call GET /voice-conversions/{id} for fresh URL
            // This prevents expired URLs from being stored in webhook logs
        }
        else if (eventType == WebhookEvent.ConversionFailed)
        {
            conversionData["error_message"] = conversion.ErrorMessage;
            conversionData["retry_count"] = conversion.RetryCount;
        }

        // Build final payload
        return new
        {
            @event = eventName,
            id = Guid.NewGuid(), // Unique event ID for idempotency
            timestamp = DateTimeOffset.UtcNow.ToString("o"),
            data = new
            {
                conversion = conversionData
            }
        };
    }
}
