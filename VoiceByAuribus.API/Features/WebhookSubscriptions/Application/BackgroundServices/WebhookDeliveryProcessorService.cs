using Microsoft.EntityFrameworkCore;
using VoiceByAuribus_API.Features.WebhookSubscriptions.Application.Services;
using VoiceByAuribus_API.Features.WebhookSubscriptions.Domain;
using VoiceByAuribus_API.Shared.Infrastructure.Data;
using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Features.WebhookSubscriptions.Application.BackgroundServices;

/// <summary>
/// Background service that processes pending and failed webhook deliveries with retry logic.
/// Runs continuously, polling for deliveries that need to be attempted or retried.
/// </summary>
public class WebhookDeliveryProcessorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WebhookDeliveryProcessorService> _logger;
    private readonly int _intervalSeconds;
    private readonly int _batchSize;
    private readonly int _maxRetryAttempts;
    private readonly int _processingTimeoutMinutes;

    public WebhookDeliveryProcessorService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<WebhookDeliveryProcessorService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _intervalSeconds = configuration.GetValue("Webhooks:BackgroundProcessor:IntervalSeconds", 5);
        _batchSize = configuration.GetValue("Webhooks:BackgroundProcessor:BatchSize", 20);
        _maxRetryAttempts = configuration.GetValue("Webhooks:Client:MaxRetryAttempts", 5);
        _processingTimeoutMinutes = configuration.GetValue("Webhooks:BackgroundProcessor:ProcessingTimeoutMinutes", 5);

        _logger.LogInformation(
            "[WEBHOOK PROCESSOR] Initialized - Interval={Interval}s, BatchSize={BatchSize}, MaxRetries={MaxRetries}, ProcessingTimeout={ProcessingTimeout}min",
            _intervalSeconds, _batchSize, _maxRetryAttempts, _processingTimeoutMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[WEBHOOK PROCESSOR] Starting background service");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingDeliveriesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WEBHOOK PROCESSOR] Error in processing loop");
            }

            // Wait before next iteration
            await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
        }

        _logger.LogInformation("[WEBHOOK PROCESSOR] Background service stopped");
    }

    /// <summary>
    /// Processes pending webhook deliveries in batches.
    /// Uses Optimistic Locking to prevent race conditions when multiple API instances are running.
    /// </summary>
    private async Task ProcessPendingDeliveriesAsync(CancellationToken cancellationToken)
    {
        var totalProcessed = 0;
        var totalSkipped = 0;
        var batchCount = 0;
        var hasMorePending = true;

        // Process all pending deliveries in batches until no more are found
        while (hasMorePending)
        {
            batchCount++;

            // Find delivery IDs that need processing
            // Using AsNoTracking for initial read (Optimistic Locking pattern)
            List<Guid> pendingDeliveryIds;
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
                var utcNow = dateTimeProvider.UtcNow;

                // Only select webhooks that are NOT being processed by another instance
                // Processing status means another instance is currently making the HTTP call
                // ALSO recover stuck webhooks that have been "Processing" for too long (likely crashed instance)
                var processingTimeoutThreshold = utcNow.AddMinutes(-_processingTimeoutMinutes);
                
                pendingDeliveryIds = await context.WebhookDeliveryLogs
                    .AsNoTracking()
                    .Include(d => d.WebhookSubscription)
                    .Where(d => (
                                    // Normal pending/failed webhooks
                                    (d.Status == WebhookDeliveryStatus.Pending || d.Status == WebhookDeliveryStatus.Failed) ||
                                    // Stuck Processing webhooks (instance likely crashed)
                                    (d.Status == WebhookDeliveryStatus.Processing && 
                                     d.AttemptedAt != null && 
                                     d.AttemptedAt.Value < processingTimeoutThreshold)
                                ) &&
                               d.AttemptNumber <= _maxRetryAttempts &&
                               (d.NextRetryAt == null || d.NextRetryAt <= utcNow) &&
                               d.WebhookSubscription.IsActive &&
                               !d.WebhookSubscription.IsDeleted)
                    .OrderBy(d => d.CreatedAt)
                    .Take(_batchSize)
                    .Select(d => d.Id)
                    .ToListAsync(cancellationToken);
            }

            if (!pendingDeliveryIds.Any())
            {
                hasMorePending = false;
                break;
            }

            _logger.LogInformation(
                "[WEBHOOK PROCESSOR] Processing batch {BatchNumber}: Found {Count} pending deliveries",
                batchCount, pendingDeliveryIds.Count);

            var batchProcessed = 0;
            var batchSkipped = 0;

            // Process each delivery individually with Optimistic Locking
            foreach (var deliveryId in pendingDeliveryIds)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    // Create a new scope for each delivery to ensure fresh DbContext
                    using var scope = _scopeFactory.CreateScope();
                    var processed = await ProcessSingleDeliveryAsync(scope.ServiceProvider, deliveryId, cancellationToken);
                    if (processed)
                        batchProcessed++;
                    else
                        batchSkipped++;
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Another instance already processed this delivery - skip
                    _logger.LogDebug(
                        "[WEBHOOK PROCESSOR] Delivery already processed by another instance - LogId={LogId}",
                        deliveryId);
                    batchSkipped++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[WEBHOOK PROCESSOR] Unexpected error processing delivery - LogId={LogId}",
                        deliveryId);
                    batchSkipped++;
                }
            }

            totalProcessed += batchProcessed;
            totalSkipped += batchSkipped;

            _logger.LogInformation(
                "[WEBHOOK PROCESSOR] Batch {BatchNumber} completed: Processed={Processed}, Skipped={Skipped}",
                batchCount, batchProcessed, batchSkipped);

            // If we processed less than batch size, it means there are no more pending
            // (or all remaining ones are being processed by other instances)
            if (pendingDeliveryIds.Count < _batchSize)
            {
                hasMorePending = false;
            }
        }

        if (totalProcessed > 0 || totalSkipped > 0)
        {
            _logger.LogInformation(
                "[WEBHOOK PROCESSOR] Background processing completed: TotalBatches={Batches}, TotalProcessed={Processed}, TotalSkipped={Skipped}",
                batchCount, totalProcessed, totalSkipped);
        }
    }

    /// <summary>
    /// Processes a single webhook delivery with Optimistic Locking to prevent race conditions.
    /// Returns true if processed, false if skipped.
    /// </summary>
    private async Task<bool> ProcessSingleDeliveryAsync(
        IServiceProvider serviceProvider,
        Guid deliveryId,
        CancellationToken cancellationToken = default)
    {
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
        var deliveryService = serviceProvider.GetRequiredService<IWebhookDeliveryService>();
        var secretService = serviceProvider.GetRequiredService<IWebhookSecretService>();
        var dateTimeProvider = serviceProvider.GetRequiredService<IDateTimeProvider>();

        // Load delivery with related data in a new transaction
        var delivery = await context.WebhookDeliveryLogs
            .Include(d => d.WebhookSubscription)
            .FirstOrDefaultAsync(d => d.Id == deliveryId, cancellationToken);

        if (delivery is null)
        {
            _logger.LogWarning(
                "[WEBHOOK PROCESSOR] Delivery not found - LogId={LogId}",
                deliveryId);
            return false;
        }

        // Double-check status (might have changed since initial query)
        // CRITICAL: Only allow Pending/Failed here, OR Processing with old AttemptedAt (stuck webhook recovery)
        // If status is Processing with recent AttemptedAt, it means another instance grabbed it → skip
        var isStuckWebhook = delivery.Status == WebhookDeliveryStatus.Processing &&
                            delivery.AttemptedAt != null &&
                            delivery.AttemptedAt.Value < dateTimeProvider.UtcNow.AddMinutes(-_processingTimeoutMinutes);

        if (delivery.Status != WebhookDeliveryStatus.Pending && 
            delivery.Status != WebhookDeliveryStatus.Failed &&
            !isStuckWebhook)
        {
            _logger.LogDebug(
                "[WEBHOOK PROCESSOR] Delivery status changed (another instance grabbed it), skipping - LogId={LogId}, Status={Status}",
                deliveryId, delivery.Status);
            return false;
        }

        // Log if recovering a stuck webhook
        if (isStuckWebhook)
        {
            var minutesStuck = delivery.AttemptedAt.HasValue 
                ? (dateTimeProvider.UtcNow - delivery.AttemptedAt.Value).TotalMinutes 
                : 0;
            
            _logger.LogWarning(
                "[WEBHOOK PROCESSOR] Recovering stuck webhook (instance likely crashed) - LogId={LogId}, AttemptedAt={AttemptedAt}, MinutesStuck={Minutes}",
                deliveryId, delivery.AttemptedAt, minutesStuck);
        }

        // Double-check subscription is still active
        if (!delivery.WebhookSubscription.IsActive || delivery.WebhookSubscription.IsDeleted)
        {
            _logger.LogDebug(
                "[WEBHOOK PROCESSOR] Subscription inactive, skipping - LogId={LogId}, SubscriptionId={SubscriptionId}",
                deliveryId, delivery.WebhookSubscriptionId);
            return false;
        }

        // CRITICAL: Mark as Processing BEFORE making HTTP call
        // This prevents other instances from processing the same webhook
        delivery.Status = WebhookDeliveryStatus.Processing;
        delivery.AttemptedAt = dateTimeProvider.UtcNow;

        try
        {
            // Save Processing status with Optimistic Locking
            // If another instance already changed this webhook, DbUpdateConcurrencyException will be thrown
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another instance already grabbed this webhook - skip
            _logger.LogDebug(
                "[WEBHOOK PROCESSOR] Delivery already grabbed by another instance - LogId={LogId}",
                deliveryId);
            return false;
        }

        // At this point, we own this webhook - safe to make HTTP call
        _logger.LogInformation(
            "[WEBHOOK PROCESSOR] Starting webhook delivery - LogId={LogId}, Attempt={Attempt}",
            delivery.Id, delivery.AttemptNumber + 1);

        try
        {
            // Decrypt the secret for HMAC signing
            var plainSecret = secretService.DecryptSecret(delivery.WebhookSubscription.EncryptedSecret);

            // Deliver the webhook
            var updatedDelivery = await deliveryService.DeliverWebhookAsync(
                delivery,
                plainSecret,
                cancellationToken);

            if (updatedDelivery.Status == WebhookDeliveryStatus.Delivered)
            {
                // Success - reset subscription failure counter
                delivery.WebhookSubscription.ConsecutiveFailures = 0;
                delivery.WebhookSubscription.LastSuccessAt = dateTimeProvider.UtcNow;

                _logger.LogInformation(
                    "[WEBHOOK PROCESSOR] Delivery successful - LogId={LogId}, SubscriptionId={SubscriptionId}",
                    delivery.Id, delivery.WebhookSubscriptionId);
            }
            else
            {
                // Failed - increment counters and schedule retry
                delivery.AttemptNumber++;
                delivery.WebhookSubscription.ConsecutiveFailures++;
                delivery.WebhookSubscription.LastFailureAt = dateTimeProvider.UtcNow;

                if (delivery.AttemptNumber > _maxRetryAttempts)
                {
                    delivery.Status = WebhookDeliveryStatus.Abandoned;
                    _logger.LogWarning(
                        "[WEBHOOK PROCESSOR] Delivery abandoned after max retries - LogId={LogId}, Attempts={Attempts}",
                        delivery.Id, delivery.AttemptNumber);
                }
                else
                {
                    // Exponential backoff: 2^attempt seconds
                    var delaySeconds = Math.Pow(2, delivery.AttemptNumber);
                    delivery.NextRetryAt = dateTimeProvider.UtcNow.AddSeconds(delaySeconds);
                    _logger.LogWarning(
                        "[WEBHOOK PROCESSOR] Delivery failed, scheduling retry - LogId={LogId}, Attempt={Attempt}, NextRetry={NextRetry}",
                        delivery.Id, delivery.AttemptNumber, delivery.NextRetryAt);
                }

                // Auto-disable subscription if too many consecutive failures
                if (delivery.WebhookSubscription.AutoDisableOnFailure &&
                    delivery.WebhookSubscription.ConsecutiveFailures >= delivery.WebhookSubscription.MaxConsecutiveFailures)
                {
                    delivery.WebhookSubscription.IsActive = false;
                    _logger.LogWarning(
                        "[WEBHOOK PROCESSOR] Auto-disabled subscription after {Failures} consecutive failures - SubscriptionId={SubscriptionId}",
                        delivery.WebhookSubscription.ConsecutiveFailures,
                        delivery.WebhookSubscription.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[WEBHOOK PROCESSOR] Error processing delivery - LogId={LogId}",
                delivery.Id);

            delivery.Status = WebhookDeliveryStatus.Failed;
            delivery.ErrorMessage = $"Processing error: {ex.Message}";
            delivery.AttemptNumber++;

            // Schedule retry if not exceeded max attempts
            if (delivery.AttemptNumber <= _maxRetryAttempts)
            {
                var delaySeconds = Math.Pow(2, delivery.AttemptNumber);
                delivery.NextRetryAt = dateTimeProvider.UtcNow.AddSeconds(delaySeconds);
            }
            else
            {
                delivery.Status = WebhookDeliveryStatus.Abandoned;
            }
        }

        // Save changes with Optimistic Locking
        // If another instance modified this record, DbUpdateConcurrencyException will be thrown
        await context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
