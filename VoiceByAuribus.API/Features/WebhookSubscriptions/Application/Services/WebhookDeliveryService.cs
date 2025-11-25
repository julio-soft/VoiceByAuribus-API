using System.Diagnostics;
using System.Text;
using VoiceByAuribus_API.Features.WebhookSubscriptions.Domain;
using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Features.WebhookSubscriptions.Application.Services;

/// <summary>
/// Service for delivering webhooks to external client endpoints via HTTP.
/// </summary>
public class WebhookDeliveryService(
    IHttpClientFactory httpClientFactory,
    IWebhookSecretService secretService,
    IDateTimeProvider dateTimeProvider,
    ILogger<WebhookDeliveryService> logger) : IWebhookDeliveryService
{
    /// <inheritdoc />
    public async Task<WebhookDeliveryLog> DeliverWebhookAsync(
        WebhookDeliveryLog deliveryLog,
        string secret,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            logger.LogInformation(
                "[WEBHOOK] Attempting delivery - LogId={LogId}, Attempt={Attempt}, URL={Url}",
                deliveryLog.Id, deliveryLog.AttemptNumber, deliveryLog.WebhookSubscription.Url);

            // Prepare payload with timestamp
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var payload = $"{timestamp}.{deliveryLog.PayloadJson}";
            var signature = secretService.ComputeHmacSignature(secret, payload);

            // Create HTTP request
            var httpClient = httpClientFactory.CreateClient("WebhookClient");
            using var request = new HttpRequestMessage(HttpMethod.Post, deliveryLog.WebhookSubscription.Url);

            request.Content = new StringContent(deliveryLog.PayloadJson, Encoding.UTF8, "application/json");
            request.Headers.Add("X-Webhook-Signature", $"sha256={signature}");
            request.Headers.Add("X-Webhook-Timestamp", timestamp.ToString());
            request.Headers.Add("X-Webhook-Id", deliveryLog.Id.ToString());
            request.Headers.Add("X-Webhook-Event", deliveryLog.Event.ToString().ToSnakeCase());

            // Send request
            using var response = await httpClient.SendAsync(request, cancellationToken);

            stopwatch.Stop();
            deliveryLog.DurationMs = (int)stopwatch.ElapsedMilliseconds;
            deliveryLog.AttemptedAt = dateTimeProvider.UtcNow;
            deliveryLog.HttpStatusCode = (int)response.StatusCode;

            // Read response body (truncate to 2000 chars)
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            deliveryLog.ResponseBody = responseBody.Length > 2000
                ? responseBody[..2000]
                : responseBody;

            // Check if successful (2xx status codes)
            if (response.IsSuccessStatusCode)
            {
                deliveryLog.Status = WebhookDeliveryStatus.Delivered;
                deliveryLog.NextRetryAt = null;
                deliveryLog.ErrorMessage = null;

                logger.LogInformation(
                    "[WEBHOOK] Delivery successful - LogId={LogId}, StatusCode={StatusCode}, Duration={Duration}ms",
                    deliveryLog.Id, deliveryLog.HttpStatusCode, deliveryLog.DurationMs);
            }
            else
            {
                deliveryLog.Status = WebhookDeliveryStatus.Failed;
                deliveryLog.ErrorMessage = $"HTTP {response.StatusCode}: {response.ReasonPhrase}";

                logger.LogWarning(
                    "[WEBHOOK] Delivery failed - LogId={LogId}, StatusCode={StatusCode}, Duration={Duration}ms",
                    deliveryLog.Id, deliveryLog.HttpStatusCode, deliveryLog.DurationMs);
            }
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            deliveryLog.DurationMs = (int)stopwatch.ElapsedMilliseconds;
            deliveryLog.AttemptedAt = dateTimeProvider.UtcNow;
            deliveryLog.Status = WebhookDeliveryStatus.Failed;
            deliveryLog.ErrorMessage = $"Network error: {ex.Message}";

            logger.LogError(ex,
                "[WEBHOOK] Delivery failed with network error - LogId={LogId}",
                deliveryLog.Id);
        }
        catch (TaskCanceledException ex)
        {
            stopwatch.Stop();
            deliveryLog.DurationMs = (int)stopwatch.ElapsedMilliseconds;
            deliveryLog.AttemptedAt = dateTimeProvider.UtcNow;
            deliveryLog.Status = WebhookDeliveryStatus.Failed;
            deliveryLog.ErrorMessage = "Request timeout";

            logger.LogError(ex,
                "[WEBHOOK] Delivery timed out - LogId={LogId}",
                deliveryLog.Id);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            deliveryLog.DurationMs = (int)stopwatch.ElapsedMilliseconds;
            deliveryLog.AttemptedAt = dateTimeProvider.UtcNow;
            deliveryLog.Status = WebhookDeliveryStatus.Failed;
            deliveryLog.ErrorMessage = $"Unexpected error: {ex.Message}";

            logger.LogError(ex,
                "[WEBHOOK] Delivery failed with unexpected error - LogId={LogId}",
                deliveryLog.Id);
        }

        return deliveryLog;
    }
}

/// <summary>
/// Helper extensions for webhook delivery.
/// </summary>
public static class WebhookStringExtensions
{
    /// <summary>
    /// Converts PascalCase to snake_case.
    /// </summary>
    public static string ToSnakeCase(this string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;

        return string.Concat(str.Select((x, i) => i > 0 && char.IsUpper(x)
            ? "_" + x
            : x.ToString())).ToLowerInvariant();
    }
}
