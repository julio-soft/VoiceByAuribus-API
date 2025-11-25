using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VoiceByAuribus_API.Features.WebhookSubscriptions.Application.Dtos;
using VoiceByAuribus_API.Features.WebhookSubscriptions.Application.Mappers;
using VoiceByAuribus_API.Features.WebhookSubscriptions.Domain;
using VoiceByAuribus_API.Shared.Domain;
using VoiceByAuribus_API.Shared.Infrastructure.Data;
using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Features.WebhookSubscriptions.Application.Services;

/// <summary>
/// Service for managing webhook subscriptions with CRUD operations.
/// </summary>
public class WebhookSubscriptionService(
    ApplicationDbContext context,
    IWebhookSecretService secretService,
    IWebhookDeliveryService deliveryService,
    IDateTimeProvider dateTimeProvider,
    IConfiguration configuration,
    ILogger<WebhookSubscriptionService> logger) : IWebhookSubscriptionService
{
    private readonly int _maxSubscriptionsPerUser = configuration.GetValue("Webhooks:Client:MaxSubscriptionsPerUser", 5);

    /// <inheritdoc />
    public async Task<CreatedWebhookSubscriptionResponseDto> CreateSubscriptionAsync(
        CreateWebhookSubscriptionDto dto,
        Guid userId)
    {
        // Check subscription limit
        var existingCount = await context.WebhookSubscriptions
            .CountAsync(s => s.UserId == userId && s.IsActive);

        if (existingCount >= _maxSubscriptionsPerUser)
        {
            throw new InvalidOperationException(
                $"Maximum number of active subscriptions ({_maxSubscriptionsPerUser}) reached. " +
                "Please delete or deactivate an existing subscription before creating a new one.");
        }

        // Generate a secure random secret (32 bytes = 64 hex characters)
        var plainSecret = secretService.GenerateSecret();

        // Encrypt the secret for storage
        var encryptedSecret = secretService.EncryptSecret(plainSecret);

        // Create subscription
        var subscription = new WebhookSubscription
        {
            Url = dto.Url,
            EncryptedSecret = encryptedSecret,
            Description = dto.Description,
            SubscribedEvents = dto.Events,
            IsActive = true,
            UserId = userId
        };

        context.WebhookSubscriptions.Add(subscription);
        await context.SaveChangesAsync();

        logger.LogInformation(
            "[WEBHOOK] Subscription created - SubscriptionId={SubscriptionId}, UserId={UserId}, URL={Url}",
            subscription.Id, userId, subscription.Url);

        return subscription.ToCreatedResponseDto(plainSecret);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<WebhookSubscriptionResponseDto>> GetUserSubscriptionsAsync(Guid userId)
    {
        var subscriptions = await context.WebhookSubscriptions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        return subscriptions.Select(s => s.ToResponseDto());
    }

    /// <inheritdoc />
    public async Task<WebhookSubscriptionResponseDto?> GetSubscriptionByIdAsync(Guid id, Guid userId)
    {
        var subscription = await context.WebhookSubscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

        return subscription?.ToResponseDto();
    }

    /// <inheritdoc />
    public async Task<WebhookSubscriptionResponseDto?> UpdateSubscriptionAsync(
        Guid id,
        UpdateWebhookSubscriptionDto dto,
        Guid userId)
    {
        var subscription = await context.WebhookSubscriptions
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

        if (subscription is null)
            return null;

        // Update only provided fields
        if (dto.Url is not null)
            subscription.Url = dto.Url;

        if (dto.Description is not null)
            subscription.Description = dto.Description;

        if (dto.Events is not null)
            subscription.SubscribedEvents = dto.Events;

        if (dto.IsActive.HasValue)
        {
            subscription.IsActive = dto.IsActive.Value;

            // Reset consecutive failures if reactivating
            if (dto.IsActive.Value)
            {
                subscription.ConsecutiveFailures = 0;
                logger.LogInformation(
                    "[WEBHOOK] Subscription reactivated - SubscriptionId={SubscriptionId}",
                    subscription.Id);
            }
        }

        await context.SaveChangesAsync();

        logger.LogInformation(
            "[WEBHOOK] Subscription updated - SubscriptionId={SubscriptionId}",
            subscription.Id);

        return subscription.ToResponseDto();
    }

    /// <inheritdoc />
    public async Task<bool> DeleteSubscriptionAsync(Guid id, Guid userId)
    {
        var subscription = await context.WebhookSubscriptions
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

        if (subscription is null)
            return false;

        // Soft delete
        subscription.IsDeleted = true;
        await context.SaveChangesAsync();

        logger.LogInformation(
            "[WEBHOOK] Subscription deleted - SubscriptionId={SubscriptionId}",
            subscription.Id);

        return true;
    }

    /// <inheritdoc />
    public async Task<RegenerateSecretResponseDto> RegenerateSecretAsync(Guid id, Guid userId)
    {
        var subscription = await context.WebhookSubscriptions
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

        if (subscription is null)
        {
            throw new InvalidOperationException("Webhook subscription not found");
        }

        // Generate new secret (32 random bytes = 64 hex characters)
        var newSecret = secretService.GenerateSecret();

        // Encrypt and store
        subscription.EncryptedSecret = secretService.EncryptSecret(newSecret);

        // Reset failure counters since we're starting fresh
        subscription.ConsecutiveFailures = 0;
        subscription.LastFailureAt = null;

        await context.SaveChangesAsync();

        logger.LogInformation(
            "[WEBHOOK] Secret regenerated - SubscriptionId={SubscriptionId}",
            subscription.Id);

        return new RegenerateSecretResponseDto
        {
            NewSecret = newSecret,
            Message = "Secret regenerated successfully. Please update your webhook handler with the new secret. " +
                     "The old secret is now invalid. This is the ONLY time the new secret will be shown."
        };
    }

    /// <inheritdoc />
    public async Task<WebhookTestResultDto> TestWebhookAsync(Guid id, Guid userId)
    {
        var subscription = await context.WebhookSubscriptions
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

        if (subscription is null)
        {
            throw new InvalidOperationException("Webhook subscription not found");
        }

        if (!subscription.IsActive)
        {
            throw new InvalidOperationException("Cannot test an inactive webhook subscription. Please activate it first.");
        }

        // Create test webhook payload
        var testPayload = new
        {
            @event = "webhook.test",
            id = Guid.NewGuid(),
            timestamp = dateTimeProvider.UtcNow.ToString("o"),
            data = new
            {
                message = "This is a test webhook from VoiceByAuribus API",
                subscription_id = subscription.Id
            }
        };

        var payloadJson = JsonSerializer.Serialize(testPayload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        });

        // Decrypt the secret for HMAC signing
        var plainSecret = secretService.DecryptSecret(subscription.EncryptedSecret);

        // Create a temporary delivery log for the test (NOT saved to DB)
        var deliveryLog = new WebhookDeliveryLog
        {
            WebhookSubscriptionId = subscription.Id,
            WebhookSubscription = subscription,
            EventType = WebhookEventTypes.Test,
            EntityType = WebhookEntityTypes.Test,
            EntityId = null,
            Event = WebhookEvent.ConversionCompleted, // Using as placeholder
            Status = WebhookDeliveryStatus.Pending,
            PayloadJson = payloadJson,
            AttemptNumber = 1
        };

        // Fire-and-forget: Send test webhook in background without blocking the request
        // This prevents test failures from disabling the subscription or cluttering the delivery logs
        _ = Task.Run(async () =>
        {
            try
            {
                await deliveryService.DeliverWebhookAsync(
                    deliveryLog,
                    plainSecret,
                    CancellationToken.None);

                logger.LogInformation(
                    "[WEBHOOK] Test webhook delivered successfully - SubscriptionId={SubscriptionId}, URL={Url}",
                    subscription.Id, subscription.Url);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "[WEBHOOK] Test webhook delivery failed - SubscriptionId={SubscriptionId}, URL={Url}",
                    subscription.Id, subscription.Url);
            }
        });

        logger.LogInformation(
            "[WEBHOOK] Test webhook queued - SubscriptionId={SubscriptionId}",
            subscription.Id);

        // Return immediate response
        return new WebhookTestResultDto
        {
            Message = "Test webhook queued successfully. Check your webhook endpoint to verify delivery.",
            Url = subscription.Url,
            TestPayload = testPayload
        };
    }

    /// <inheritdoc />
    public async Task<IEnumerable<WebhookDeliveryLogResponseDto>> GetDeliveryLogsAsync(
        Guid subscriptionId,
        Guid userId,
        int limit = 100)
    {
        // Verify ownership
        var subscription = await context.WebhookSubscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == subscriptionId && s.UserId == userId);

        if (subscription is null)
        {
            return Enumerable.Empty<WebhookDeliveryLogResponseDto>();
        }

        var logs = await context.WebhookDeliveryLogs
            .Where(l => l.WebhookSubscriptionId == subscriptionId)
            .OrderByDescending(l => l.CreatedAt)
            .Take(Math.Min(limit, 500)) // Max 500 logs
            .AsNoTracking()
            .ToListAsync();

        return logs.Select(log => log.ToDeliveryLogDto());
    }
}
