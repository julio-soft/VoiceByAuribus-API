---
sidebar_position: 2
---

# Webhook Notifications

Receive real-time notifications about your voice conversion jobs via webhooks.

## Overview

Webhooks allow your application to receive instant notifications when events occur in the VoiceByAuribus system. Instead of polling the API for updates, the system will send HTTP POST requests to your configured endpoint when conversions complete or fail.

### Benefits

- **Real-time Updates**: Receive notifications immediately when conversions complete
- **Reduced Polling**: No need to continuously check conversion status
- **Efficient Processing**: React to events as they happen
- **Scalable**: Handle high volumes of conversions without API rate limits

## Supported Events

| Event | Description |
|-------|-------------|
| `conversion.completed` | A voice conversion has successfully completed |
| `conversion.failed` | A voice conversion has failed to process |

## Creating a Webhook Subscription

### Step 1: Prepare Your Endpoint

Your webhook endpoint must:

- Be publicly accessible over **HTTPS** (HTTP is not supported for security)
- Respond with HTTP `200-299` status code within 30 seconds
- Handle duplicate deliveries idempotently (same event may be delivered multiple times)

### Step 2: Subscribe to Events

Create a webhook subscription by specifying which events you want to receive:

```bash
curl -X POST https://api.auribus.io/api/v1/webhook-subscriptions \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "url": "https://your-app.com/webhooks/voice-conversions",
    "events": ["conversion.completed", "conversion.failed"]
  }'
```

**Response**:
```json
{
  "success": true,
  "data": {
    "id": "880e8400-e29b-41d4-a716-446655440003",
    "url": "https://your-app.com/webhooks/voice-conversions",
    "events": ["conversion.completed", "conversion.failed"],
    "is_active": true,
    "secret": "whsec_a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0u1v2w3x4y5z6a7b8c9d0e1f2",
    "created_at": "2025-01-15T09:00:00Z"
  }
}
```

:::warning Important - Save Your Secret
The `secret` is shown **only once** when you create or regenerate a subscription. Save it securely - you'll need it to verify webhook signatures. If you lose it, you can regenerate it using the `/regenerate-secret` endpoint.
:::

### Subscription Limits

- Maximum **5 active subscriptions** per account
- Subscriptions are automatically disabled after **10 consecutive delivery failures**

## Receiving Webhook Notifications

### Webhook Payload Structure

All webhook notifications follow this structure:

```json
{
  "event": "conversion.completed",
  "timestamp": "2025-01-15T10:45:00Z",
  "data": {
    // Event-specific data (see below)
  }
}
```

### conversion.completed Event

Sent when a voice conversion successfully completes:

```json
{
  "event": "conversion.completed",
  "timestamp": "2025-01-15T10:45:00Z",
  "data": {
    "id": "770e8400-e29b-41d4-a716-446655440002",
    "audio_file_id": "660e8400-e29b-41d4-a716-446655440001",
    "voice_model_id": "550e8400-e29b-41d4-a716-446655440000",
    "pitch_shift": "same_octave",
    "status": "completed",
    "completed_at": "2025-01-15T10:45:00Z"
  }
}
```

:::tip Getting Download URLs
The webhook payload does **not** include the download URL (`output_url`). To download the converted audio, call `GET /api/v1/voice-conversions/{id}` with the conversion ID from the webhook. This ensures you always get a fresh, valid URL.
:::

### conversion.failed Event

Sent when a voice conversion fails to process:

```json
{
  "event": "conversion.failed",
  "timestamp": "2025-01-15T10:45:00Z",
  "data": {
    "id": "770e8400-e29b-41d4-a716-446655440002",
    "audio_file_id": "660e8400-e29b-41d4-a716-446655440001",
    "voice_model_id": "550e8400-e29b-41d4-a716-446655440000",
    "pitch_shift": "same_octave",
    "status": "failed"
  }
}
```

## Verifying Webhook Signatures

**All webhook notifications are signed with HMAC-SHA256** to ensure they came from VoiceByAuribus. You must verify the signature to prevent unauthorized requests.

### How Signature Verification Works

1. VoiceByAuribus sends the raw request body and an `X-Webhook-Signature` header
2. You compute HMAC-SHA256 of the raw body using your webhook secret
3. Compare your computed signature with the header value
4. Reject the request if signatures don't match

### Signature Header Format

```
X-Webhook-Signature: sha256=<hex-encoded-signature>
```

### Verification Examples

#### Node.js / TypeScript

```typescript
import crypto from 'crypto';

function verifyWebhookSignature(
  rawBody: string,
  signature: string,
  secret: string
): boolean {
  // Remove 'sha256=' prefix
  const receivedSignature = signature.replace('sha256=', '');

  // Compute HMAC-SHA256
  const hmac = crypto.createHmac('sha256', secret);
  hmac.update(rawBody);
  const computedSignature = hmac.digest('hex');

  // Constant-time comparison to prevent timing attacks
  return crypto.timingSafeEqual(
    Buffer.from(computedSignature),
    Buffer.from(receivedSignature)
  );
}

// Express.js example
app.post('/webhooks/voice-conversions', express.raw({ type: 'application/json' }), (req, res) => {
  const signature = req.headers['x-webhook-signature'] as string;
  const rawBody = req.body.toString('utf8');
  const secret = process.env.WEBHOOK_SECRET!;

  if (!verifyWebhookSignature(rawBody, signature, secret)) {
    console.error('Invalid webhook signature');
    return res.status(401).send('Unauthorized');
  }

  // Parse and process the webhook
  const event = JSON.parse(rawBody);
  console.log('Valid webhook received:', event);

  res.status(200).send('OK');
});
```

#### Python

```python
import hmac
import hashlib

def verify_webhook_signature(raw_body: bytes, signature: str, secret: str) -> bool:
    """Verify HMAC-SHA256 signature of webhook payload."""
    # Remove 'sha256=' prefix
    received_signature = signature.replace('sha256=', '')

    # Compute HMAC-SHA256
    computed_signature = hmac.new(
        secret.encode('utf-8'),
        raw_body,
        hashlib.sha256
    ).hexdigest()

    # Constant-time comparison
    return hmac.compare_digest(computed_signature, received_signature)

# Flask example
from flask import Flask, request, jsonify

@app.route('/webhooks/voice-conversions', methods=['POST'])
def webhook():
    signature = request.headers.get('X-Webhook-Signature')
    raw_body = request.get_data()
    secret = os.environ['WEBHOOK_SECRET']

    if not verify_webhook_signature(raw_body, signature, secret):
        return jsonify({'error': 'Unauthorized'}), 401

    # Parse and process the webhook
    event = request.get_json()
    print(f"Valid webhook received: {event}")

    return '', 200
```

#### C# / .NET

```csharp
using System.Security.Cryptography;
using System.Text;

public class WebhookValidator
{
    public static bool VerifyWebhookSignature(string rawBody, string signature, string secret)
    {
        // Remove 'sha256=' prefix
        var receivedSignature = signature.Replace("sha256=", "");

        // Compute HMAC-SHA256
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        var computedSignature = BitConverter.ToString(hash).Replace("-", "").ToLower();

        // Constant-time comparison
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedSignature),
            Encoding.UTF8.GetBytes(receivedSignature)
        );
    }
}

// ASP.NET Core example
[HttpPost("webhooks/voice-conversions")]
public async Task<IActionResult> ReceiveWebhook()
{
    using var reader = new StreamReader(Request.Body);
    var rawBody = await reader.ReadToEndAsync();

    var signature = Request.Headers["X-Webhook-Signature"].ToString();
    var secret = Configuration["WebhookSecret"];

    if (!WebhookValidator.VerifyWebhookSignature(rawBody, signature, secret))
    {
        return Unauthorized(new { error = "Invalid signature" });
    }

    var webhookEvent = JsonSerializer.Deserialize<WebhookEvent>(rawBody);
    Console.WriteLine($"Valid webhook received: {webhookEvent.Event}");

    return Ok();
}
```

#### PHP

```php
<?php

function verifyWebhookSignature(string $rawBody, string $signature, string $secret): bool {
    // Remove 'sha256=' prefix
    $receivedSignature = str_replace('sha256=', '', $signature);

    // Compute HMAC-SHA256
    $computedSignature = hash_hmac('sha256', $rawBody, $secret);

    // Constant-time comparison
    return hash_equals($computedSignature, $receivedSignature);
}

// Example usage
$rawBody = file_get_contents('php://input');
$signature = $_SERVER['HTTP_X_WEBHOOK_SIGNATURE'] ?? '';
$secret = getenv('WEBHOOK_SECRET');

if (!verifyWebhookSignature($rawBody, $signature, $secret)) {
    http_response_code(401);
    echo json_encode(['error' => 'Unauthorized']);
    exit;
}

$event = json_decode($rawBody, true);
error_log('Valid webhook received: ' . $event['event']);

http_response_code(200);
echo 'OK';
```

## Testing Your Webhook

### Test Endpoint

Use the test endpoint to verify your webhook is configured correctly:

```bash
curl -X POST https://api.auribus.io/api/v1/webhook-subscriptions/{subscription_id}/test \
  -H "Authorization: Bearer YOUR_TOKEN"
```

This sends a test event to your webhook endpoint:

```json
{
  "event": "webhook.test",
  "timestamp": "2025-01-15T10:00:00Z",
  "data": {
    "message": "This is a test webhook from VoiceByAuribus"
  }
}
```

:::info Test Webhooks
Test webhooks are **not saved to the delivery log** and do **not count toward auto-disable failures**. They're sent immediately to verify your endpoint is working correctly.
:::

## Best Practices

### 1. Implement Idempotency

The same webhook may be delivered multiple times due to retries. Use the conversion `id` to track which events you've already processed:

```typescript
const processedEvents = new Set<string>();

app.post('/webhooks/voice-conversions', async (req, res) => {
  const event = req.body;
  const eventId = event.data.id;

  // Check if already processed
  if (processedEvents.has(eventId)) {
    console.log('Duplicate webhook, skipping');
    return res.status(200).send('OK');
  }

  // Process the event
  await handleConversion(event.data);

  // Mark as processed
  processedEvents.add(eventId);

  res.status(200).send('OK');
});
```

### 2. Respond Quickly

Your endpoint should respond with `200 OK` as quickly as possible (within 30 seconds). Process the webhook asynchronously:

```typescript
app.post('/webhooks/voice-conversions', async (req, res) => {
  // Respond immediately
  res.status(200).send('OK');

  // Process asynchronously
  setImmediate(async () => {
    try {
      await processWebhook(req.body);
    } catch (error) {
      console.error('Error processing webhook:', error);
    }
  });
});
```

### 3. Handle Failures Gracefully

If your endpoint returns an error status code (4xx or 5xx), VoiceByAuribus will retry the webhook:

- **Retry Schedule**: Exponential backoff (2s, 4s, 8s, 16s, 32s)
- **Max Attempts**: 5 retries
- **Auto-Disable**: After 10 consecutive failures across all events

```typescript
app.post('/webhooks/voice-conversions', async (req, res) => {
  try {
    await processWebhook(req.body);
    res.status(200).send('OK');
  } catch (error) {
    console.error('Error processing webhook:', error);
    // Return 500 to trigger retry
    res.status(500).send('Internal Server Error');
  }
});
```

### 4. Monitor Webhook Health

Regularly check your webhook subscription status:

```bash
curl -X GET https://api.auribus.io/api/v1/webhook-subscriptions \
  -H "Authorization: Bearer YOUR_TOKEN"
```

If `is_active: false`, your subscription was disabled due to failures. Fix your endpoint and create a new subscription.

### 5. Secure Your Endpoint

- **Always verify signatures**: Never process webhooks without signature verification
- **Use HTTPS only**: Plain HTTP is rejected by VoiceByAuribus
- **Rate limiting**: Implement rate limiting to prevent abuse
- **Authentication**: Consider adding your own authentication on top of signature verification

## Troubleshooting

### Webhooks Not Being Received

1. **Check subscription is active**: `GET /api/v1/webhook-subscriptions`
2. **Verify URL is publicly accessible**: Test with tools like `curl` from an external server
3. **Ensure HTTPS**: HTTP URLs are rejected
4. **Check firewall rules**: Make sure your server accepts incoming connections
5. **Review server logs**: Look for errors or connection timeouts

### Signature Verification Failing

1. **Use raw request body**: Don't parse JSON before verifying the signature
2. **Check secret**: Ensure you're using the correct webhook secret
3. **Match encoding**: Signature is computed on UTF-8 encoded raw body
4. **Constant-time comparison**: Use `crypto.timingSafeEqual()` or equivalent
5. **Check header name**: Header is `X-Webhook-Signature` (case-insensitive)

### Subscription Disabled

If your subscription shows `is_active: false`:

1. **Review recent failures**: Check your server logs for webhook delivery errors
2. **Fix your endpoint**: Ensure it's responding with `200 OK` status codes
3. **Create new subscription**: Disabled subscriptions cannot be re-enabled; create a new one

### Missing Download URL

The webhook payload intentionally **does not include** `output_url`. To download converted audio:

```bash
# Use the conversion ID from the webhook
curl -X GET https://api.auribus.io/api/v1/voice-conversions/{id} \
  -H "Authorization: Bearer YOUR_TOKEN"
```

This returns a fresh download URL that is valid for 12 hours.

## Complete Example

Here's a complete Node.js/Express example handling webhooks:

```typescript
import express from 'express';
import crypto from 'crypto';

const app = express();

// Store processed event IDs (use Redis/database in production)
const processedEvents = new Set<string>();

// Webhook secret from environment
const WEBHOOK_SECRET = process.env.WEBHOOK_SECRET!;

function verifySignature(rawBody: string, signature: string): boolean {
  const receivedSig = signature.replace('sha256=', '');
  const hmac = crypto.createHmac('sha256', WEBHOOK_SECRET);
  hmac.update(rawBody);
  const computedSig = hmac.digest('hex');

  return crypto.timingSafeEqual(
    Buffer.from(computedSig),
    Buffer.from(receivedSig)
  );
}

async function handleConversionCompleted(data: any) {
  console.log(`Conversion ${data.id} completed!`);

  // Fetch download URLs
  const response = await fetch(
    `https://api.auribus.io/api/v1/voice-conversions/${data.id}`,
    {
      headers: {
        'Authorization': `Bearer ${process.env.API_TOKEN}`
      }
    }
  );

  const conversion = await response.json();
  console.log(`Download URL: ${conversion.data.output_url}`);

  // Process the converted audio...
}

async function handleConversionFailed(data: any) {
  console.log(`Conversion ${data.id} failed`);
  // Handle failure (notify user, retry, etc.)
}

// Webhook endpoint with raw body parsing
app.post(
  '/webhooks/voice-conversions',
  express.raw({ type: 'application/json' }),
  async (req, res) => {
    try {
      // Verify signature
      const signature = req.headers['x-webhook-signature'] as string;
      const rawBody = req.body.toString('utf8');

      if (!verifySignature(rawBody, signature)) {
        console.error('Invalid webhook signature');
        return res.status(401).send('Unauthorized');
      }

      // Parse event
      const event = JSON.parse(rawBody);
      const eventId = event.data.id;

      // Check for duplicates
      if (processedEvents.has(eventId)) {
        console.log('Duplicate webhook, skipping');
        return res.status(200).send('OK');
      }

      // Respond immediately
      res.status(200).send('OK');

      // Process asynchronously
      setImmediate(async () => {
        try {
          // Handle event based on type
          switch (event.event) {
            case 'conversion.completed':
              await handleConversionCompleted(event.data);
              break;
            case 'conversion.failed':
              await handleConversionFailed(event.data);
              break;
            case 'webhook.test':
              console.log('Test webhook received');
              break;
          }

          // Mark as processed
          processedEvents.add(eventId);
        } catch (error) {
          console.error('Error processing webhook:', error);
        }
      });

    } catch (error) {
      console.error('Webhook error:', error);
      res.status(500).send('Internal Server Error');
    }
  }
);

app.listen(3000, () => {
  console.log('Webhook server running on port 3000');
});
```

## Next Steps

- [Learn about voice conversions](voice-conversion)
- [Explore API reference](../api/voicebyauribus-api)
