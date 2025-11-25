# Webhooks API v1

## Overview

The Webhooks API allows external clients to receive real-time notifications when voice conversions complete or fail. This is a subscription-based system where clients register webhook endpoints to receive HTTP POST requests with conversion event data.

## Base URL

All webhook endpoints are prefixed with:

```
/api/v1/webhooks/subscriptions
```

## Authentication

All webhook subscription management endpoints require authentication via AWS Cognito M2M token with the `voice-by-auribus-api/base` scope.

**Headers:**
```
Authorization: Bearer <cognito-m2m-token>
```

## Webhook Events

The system supports the following webhook events:

| Event | Value | Description |
|-------|-------|-------------|
| Conversion Completed | `ConversionCompleted` | Triggered when a voice conversion finishes successfully |
| Conversion Failed | `ConversionFailed` | Triggered when a voice conversion fails |

## Security

### HMAC Signature Verification

All webhook deliveries include an HMAC-SHA256 signature for payload verification. The signature is computed as:

```
HMAC-SHA256(secret, "{timestamp}.{payload}")
```

**Headers sent with webhook:**
- `X-Webhook-Signature`: `sha256={signature}` - HMAC signature in hex format
- `X-Webhook-Timestamp`: Unix timestamp of the request
- `X-Webhook-Id`: Unique delivery log ID
- `X-Webhook-Event`: Event name (e.g., "conversion.completed")

### Verification Example (Node.js)

```javascript
const crypto = require('crypto');

function verifyWebhookSignature(req, secret) {
    const signature = req.headers['x-webhook-signature'];
    const timestamp = req.headers['x-webhook-timestamp'];
    const payload = JSON.stringify(req.body);

    // Compute expected signature
    const expectedSignature = crypto
        .createHmac('sha256', secret)
        .update(`${timestamp}.${payload}`)
        .digest('hex');

    const receivedSignature = signature.replace('sha256=', '');

    // Constant-time comparison
    return crypto.timingSafeEqual(
        Buffer.from(expectedSignature),
        Buffer.from(receivedSignature)
    );
}

// Express middleware
app.post('/webhook', (req, res) => {
    if (!verifyWebhookSignature(req, process.env.WEBHOOK_SECRET)) {
        return res.status(401).json({ error: 'Invalid signature' });
    }

    // Process webhook
    console.log('Event:', req.headers['x-webhook-event']);
    console.log('Data:', req.body);

    res.status(200).json({ received: true });
});
```

### Verification Example (Python)

```python
import hmac
import hashlib

def verify_webhook_signature(request, secret):
    signature = request.headers.get('X-Webhook-Signature', '').replace('sha256=', '')
    timestamp = request.headers.get('X-Webhook-Timestamp')
    payload = request.get_data(as_text=True)

    # Compute expected signature
    message = f"{timestamp}.{payload}"
    expected_signature = hmac.new(
        secret.encode('utf-8'),
        message.encode('utf-8'),
        hashlib.sha256
    ).hexdigest()

    # Constant-time comparison
    return hmac.compare_digest(expected_signature, signature)

# Flask example
@app.route('/webhook', methods=['POST'])
def handle_webhook():
    if not verify_webhook_signature(request, os.environ['WEBHOOK_SECRET']):
        return jsonify({'error': 'Invalid signature'}), 401

    event = request.headers.get('X-Webhook-Event')
    data = request.json

    print(f'Event: {event}')
    print(f'Data: {data}')

    return jsonify({'received': True}), 200
```

## Webhook Payload Format

### Conversion Completed Event

```json
{
  "event": "conversion.completed",
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "timestamp": "2025-11-22T20:30:45.123Z",
  "data": {
    "conversion": {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "status": "completed",
      "audio_file_id": "660e8400-e29b-41d4-a716-446655440000",
      "voice_model_id": "770e8400-e29b-41d4-a716-446655440000",
      "pitch_shift": "same_octave",
      "use_preview": false,
      "queued_at": "2025-11-22T20:28:00.000Z",
      "processing_started_at": "2025-11-22T20:28:15.000Z",
      "completed_at": "2025-11-22T20:30:45.000Z",
      "processing_duration_seconds": 150
    }
  }
}
```

**Fields:**
- `event` - Event type identifier (always "conversion.completed")
- `id` - Unique event ID for idempotency tracking
- `timestamp` - ISO 8601 timestamp of event generation
- `data.conversion.id` - Voice conversion ID
- `data.conversion.status` - Conversion status (always "completed" for this event)
- `data.conversion.pitch_shift` - Pitch shift applied to the conversion. Values: `same_octave`, `lower_octave`, `higher_octave`, `third_down`, `third_up`, `fifth_down`, `fifth_up`
- `data.conversion.processing_duration_seconds` - Processing time in seconds

**Note:** The `output_url` is NOT included in the webhook payload to prevent expired URLs.
To download the output file, call `GET /api/v1/voice-conversions/{id}` which returns a fresh pre-signed URL (valid for 12 hours).

### Conversion Failed Event

```json
{
  "event": "conversion.failed",
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "timestamp": "2025-11-22T20:30:45.123Z",
  "data": {
    "conversion": {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "status": "failed",
      "audio_file_id": "660e8400-e29b-41d4-a716-446655440000",
      "voice_model_id": "770e8400-e29b-41d4-a716-446655440000",
      "pitch_shift": "same_octave",
      "use_preview": false,
      "queued_at": "2025-11-22T20:28:00.000Z",
      "processing_started_at": "2025-11-22T20:28:15.000Z",
      "completed_at": "2025-11-22T20:30:45.000Z",
      "error_message": "Voice model file not found",
      "retry_count": 3
    }
  }
}
```

**Additional Fields:**
- `data.conversion.error_message` - Error description
- `data.conversion.retry_count` - Number of processing retry attempts

## Retry Behavior

The webhook delivery system implements automatic retry with exponential backoff:

- **Max Attempts**: 5
- **Retry Delay**: 2^attempt seconds (2s, 4s, 8s, 16s, 32s)
- **Timeout**: 30 seconds per delivery attempt

**Success Criteria:**
- HTTP status code 2xx (200-299)
- Response received within 30 seconds

**Failure Scenarios:**
- Non-2xx HTTP status codes
- Network errors or timeouts
- Invalid SSL/TLS certificates
- Connection refused

After 5 failed attempts, the delivery is marked as "Abandoned" and will not be retried.

### Auto-Disable Feature

If a webhook subscription encounters consecutive failures (configurable, default: 10), it will be automatically disabled to prevent resource waste. You can re-enable it via the PATCH endpoint after fixing the issue.

## Endpoints

### 1. Create Webhook Subscription

Creates a new webhook subscription with an auto-generated secret.

**Important:** The secret is auto-generated by the system and returned ONLY in this response. It cannot be retrieved later. Save it securely.

```http
POST /api/v1/webhooks/subscriptions
Content-Type: application/json
Authorization: Bearer <token>

{
  "url": "https://example.com/webhooks/voice-conversions",
  "description": "Production webhook for conversion events",
  "events": ["ConversionCompleted", "ConversionFailed"]
}
```

**Request Body:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| url | string | Yes | HTTPS webhook endpoint URL (HTTP not allowed for security) |
| description | string | No | Human-readable description |
| events | string[] | No | Events to subscribe to (defaults to all events) |

**Validations:**
- URL must be HTTPS (HTTP rejected)
- URL cannot be localhost, 127.0.0.1, or private IP ranges (SSRF protection)
- Maximum 5 active subscriptions per user

**Response (201 Created):**

```json
{
  "success": true,
  "message": "Webhook subscription created successfully. Your secret has been auto-generated and encrypted. This is the ONLY time you will see the plain text secret - save it securely.",
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "url": "https://example.com/webhooks/voice-conversions",
    "secret": "a1b2c3d4e5f6789012345678901234567890abcdef1234567890abcdef123456",
    "description": "Production webhook for conversion events",
    "events": ["ConversionCompleted", "ConversionFailed"],
    "is_active": true,
    "consecutive_failures": 0,
    "last_success_at": null,
    "last_failure_at": null,
    "created_at": "2025-11-22T20:00:00.000Z",
    "updated_at": "2025-11-22T20:00:00.000Z"
  }
}
```

**Response Fields:**
- `secret` - **[CRITICAL]** Auto-generated 64-character hexadecimal secret. This will NEVER be shown again. Use this for HMAC signature verification.
- All other fields same as listed responses

**Error Responses:**

```json
// Maximum subscriptions reached
{
  "success": false,
  "message": "Maximum number of active subscriptions (5) reached. Please delete or deactivate an existing subscription before creating a new one.",
  "data": null
}

// Validation errors
{
  "success": false,
  "message": "Validation failed",
  "data": null,
  "errors": [
    "URL must be a valid HTTPS URL (HTTP is not allowed)",
    "URL cannot point to localhost or private IP addresses (SSRF protection)"
  ]
}
```

### 2. List Webhook Subscriptions

Retrieves all webhook subscriptions for the authenticated user.

```http
GET /api/v1/webhooks/subscriptions
Authorization: Bearer <token>
```

**Response (200 OK):**

```json
{
  "success": true,
  "data": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "url": "https://example.com/webhooks/voice-conversions",
      "description": "Production webhook",
      "subscribed_events": ["ConversionCompleted", "ConversionFailed"],
      "is_active": true,
      "consecutive_failures": 0,
      "last_success_at": "2025-11-22T19:45:00.000Z",
      "last_failure_at": null,
      "auto_disable_on_failure": true,
      "max_consecutive_failures": 10,
      "created_at": "2025-11-22T18:00:00.000Z"
    }
  ]
}
```

### 3. Get Webhook Subscription

Retrieves a specific webhook subscription by ID.

```http
GET /api/v1/webhooks/subscriptions/{id}
Authorization: Bearer <token>
```

**Response (200 OK):**

```json
{
  "success": true,
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "url": "https://example.com/webhooks/voice-conversions",
    "description": "Production webhook",
    "subscribed_events": ["ConversionCompleted"],
    "is_active": true,
    "consecutive_failures": 2,
    "last_success_at": "2025-11-22T19:00:00.000Z",
    "last_failure_at": "2025-11-22T19:30:00.000Z",
    "auto_disable_on_failure": true,
    "max_consecutive_failures": 10,
    "created_at": "2025-11-22T18:00:00.000Z"
  }
}
```

**Response (404 Not Found):**

```json
{
  "success": false,
  "message": "Webhook subscription not found",
  "data": null
}
```

### 4. Update Webhook Subscription

Updates an existing webhook subscription. You can modify the URL, description, subscribed events, or active status.

**Note:** You cannot update the secret via this endpoint. Use the regenerate-secret endpoint instead.

```http
PATCH /api/v1/webhooks/subscriptions/{id}
Content-Type: application/json
Authorization: Bearer <token>

{
  "url": "https://new-endpoint.example.com/webhooks",
  "description": "Updated webhook endpoint",
  "events": ["ConversionCompleted"],
  "is_active": true
}
```

**Request Body (all fields optional):**

| Field | Type | Description |
|-------|------|-------------|
| url | string | New webhook URL (must be HTTPS) |
| description | string | Updated description |
| events | string[] | Updated event subscriptions |
| is_active | boolean | Enable/disable the subscription |

**Response (200 OK):**

```json
{
  "success": true,
  "message": "Webhook subscription updated successfully",
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "url": "https://new-endpoint.example.com/webhooks",
    "description": "Updated webhook endpoint",
    "subscribed_events": ["ConversionCompleted"],
    "is_active": true,
    "consecutive_failures": 0,
    "last_success_at": "2025-11-22T19:00:00.000Z",
    "last_failure_at": null,
    "auto_disable_on_failure": true,
    "max_consecutive_failures": 10,
    "created_at": "2025-11-22T18:00:00.000Z"
  }
}
```

### 5. Delete Webhook Subscription

Soft-deletes a webhook subscription. All associated delivery logs are also marked as deleted.

```http
DELETE /api/v1/webhooks/subscriptions/{id}
Authorization: Bearer <token>
```

**Response (200 OK):**

```json
{
  "success": true,
  "data": {
    "Message": "Webhook subscription deleted successfully",
    "SubscriptionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
  }
}
```

### 6. Regenerate Secret

Generates a new secret for a webhook subscription. The old secret is immediately invalidated.

**Important:** The new secret is shown ONLY in this response. Save it securely.

```http
POST /api/v1/webhooks/subscriptions/{id}/regenerate-secret
Authorization: Bearer <token>
```

**Response (200 OK):**

```json
{
  "success": true,
  "message": "Secret regenerated successfully",
  "data": {
    "subscription_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "new_secret": "3d7f9a2c8e1b4d6f5a9c3e7b2d8f1a4c9e6b3d7f2a8c5e1b9d4f7a3c6e2b8d5f1a9",
    "created_at": "2025-11-22T20:15:00.000Z",
    "warning": "This is the only time the secret will be displayed. Save it securely."
  }
}
```

**Use Cases:**
- Secret was compromised or leaked
- Secret was lost
- Regular security rotation policy

### 7. Test Webhook

Sends a test webhook to verify your endpoint is reachable and configured correctly.

**Important Notes:**
- Test webhooks are sent **asynchronously** (fire-and-forget) to prevent blocking the API request
- The test webhook is **NOT saved to the database** and **will NOT be retried** by the background processor
- This prevents test failures from counting toward the auto-disable threshold (10 consecutive failures)
- The endpoint returns immediately with a confirmation message
- To verify delivery, monitor your webhook endpoint's logs
- For full end-to-end testing with retry logic, trigger an actual voice conversion

```http
POST /api/v1/webhooks/subscriptions/{id}/test
Authorization: Bearer <token>
```

**Response (200 OK):**

```json
{
  "success": true,
  "data": {
    "message": "Test webhook queued successfully. Check your webhook endpoint to verify delivery.",
    "url": "https://your-app.com/webhooks",
    "test_payload": {
      "event": "webhook.test",
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "timestamp": "2025-11-25T10:30:00.000Z",
      "data": {
        "message": "This is a test webhook from VoiceByAuribus API",
        "subscription_id": "123e4567-e89b-12d3-a456-426614174000"
      }
    }
  }
}
```

**Response (404 Not Found):**

```json
{
  "success": false,
  "message": "Webhook subscription not found"
}
```

**Response (400 Bad Request - Inactive):**

```json
{
  "success": false,
  "message": "Cannot test an inactive webhook subscription. Please activate it first."
}
```

### 8. Get Delivery Logs

Retrieves the webhook delivery history for a subscription, showing all delivery attempts with their status, response codes, and error messages.

```http
GET /api/v1/webhooks/subscriptions/{id}/deliveries?limit=100
Authorization: Bearer <token>
```

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| limit | int | 100 | Maximum number of logs to return (max 500) |

**Response (200 OK):**

```json
{
  "success": true,
  "data": [
    {
      "id": "4fa85f64-5717-4562-b3fc-2c963f66afa6",
      "voice_conversion_id": "550e8400-e29b-41d4-a716-446655440000",
      "event": "ConversionCompleted",
      "status": "Delivered",
      "attempt_number": 1,
      "http_status_code": 200,
      "response_body": "{\"received\":true}",
      "duration_ms": 156,
      "next_retry_at": null,
      "error_message": null,
      "created_at": "2025-11-22T20:30:00.000Z"
    },
    {
      "id": "5fa85f64-5717-4562-b3fc-2c963f66afa6",
      "voice_conversion_id": "560e8400-e29b-41d4-a716-446655440000",
      "event": "ConversionFailed",
      "status": "Failed",
      "attempt_number": 3,
      "http_status_code": 503,
      "response_body": null,
      "duration_ms": 30001,
      "next_retry_at": "2025-11-22T20:40:00.000Z",
      "error_message": "HTTP request timeout after 30 seconds",
      "created_at": "2025-11-22T20:25:00.000Z"
    },
    {
      "id": "6fa85f64-5717-4562-b3fc-2c963f66afa6",
      "voice_conversion_id": "570e8400-e29b-41d4-a716-446655440000",
      "event": "ConversionCompleted",
      "status": "Abandoned",
      "attempt_number": 5,
      "http_status_code": 500,
      "response_body": "{\"error\":\"Internal server error\"}",
      "duration_ms": 234,
      "next_retry_at": null,
      "error_message": "HTTP 500: Internal server error",
      "created_at": "2025-11-22T20:15:00.000Z"
    }
  ]
}
```

**Delivery Statuses:**
- `Pending` - Queued for delivery, awaiting processing
- `Delivered` - Successfully delivered (HTTP 2xx response)
- `Failed` - Delivery failed, will retry
- `Abandoned` - Failed after max retry attempts

## Best Practices

### 1. Secret Management

- **Generate Strong Secrets**: Use cryptographically secure random generators (minimum 32 characters)
- **Store Securely**: Save secrets in environment variables or secret management systems (AWS Secrets Manager, HashiCorp Vault)
- **Rotate Regularly**: Implement periodic secret rotation (e.g., every 90 days)
- **Never Commit**: Never commit secrets to version control

**Example Secret Generation (Node.js):**
```javascript
const crypto = require('crypto');
const secret = crypto.randomBytes(32).toString('hex');
console.log(secret); // 64-character hex string
```

**Example Secret Generation (Python):**
```python
import secrets
secret = secrets.token_hex(32)
print(secret)  # 64-character hex string
```

### 2. Webhook Endpoint Implementation

- **Return Quickly**: Respond with 200 OK immediately, process asynchronously
- **Idempotency**: Use the event `id` field to deduplicate events
- **Verify Signatures**: Always verify HMAC signatures before processing
- **Handle Retries**: Be idempotent to handle potential duplicate deliveries
- **Log Everything**: Log all webhook receipts for debugging and auditing

**Example (Express.js):**
```javascript
app.post('/webhook', async (req, res) => {
    // Verify signature
    if (!verifyWebhookSignature(req, process.env.WEBHOOK_SECRET)) {
        return res.status(401).json({ error: 'Invalid signature' });
    }

    // Respond immediately
    res.status(200).json({ received: true });

    // Process asynchronously
    const event = req.body;
    processWebhookAsync(event).catch(err => {
        console.error('Webhook processing error:', err);
    });
});

async function processWebhookAsync(event) {
    // Check for duplicate using event.id
    const exists = await db.webhookEvents.findOne({ eventId: event.id });
    if (exists) {
        console.log('Duplicate event, skipping:', event.id);
        return;
    }

    // Save event
    await db.webhookEvents.create({ eventId: event.id, data: event });

    // Process based on event type
    if (event.event === 'conversion.completed') {
        await handleConversionCompleted(event.data.conversion);
    } else if (event.event === 'conversion.failed') {
        await handleConversionFailed(event.data.conversion);
    }
}
```

### 3. Error Handling

- **Timeout Protection**: Set reasonable timeouts (e.g., 30 seconds)
- **Circuit Breaker**: Implement circuit breaker pattern for downstream failures
- **Monitoring**: Set up alerts for high failure rates
- **Graceful Degradation**: Have fallback mechanisms if webhooks fail

### 4. Testing

1. **Create Test Subscription**: Use a test webhook receiver (e.g., webhook.site, ngrok)
2. **Test Endpoint**: Use the `/test` endpoint to verify connectivity
3. **Trigger Real Event**: Create an actual voice conversion to test end-to-end
4. **Monitor Delivery Logs**: Check `/deliveries` endpoint for delivery status
5. **Verify Signature**: Ensure your signature verification works correctly

### 5. Production Deployment

- **HTTPS Only**: Ensure your webhook endpoint uses valid SSL/TLS certificates
- **Rate Limiting**: Implement rate limiting to prevent abuse
- **IP Whitelisting**: Consider whitelisting VoiceByAuribus API IPs (if provided)
- **Monitoring**: Monitor delivery success rates and latencies
- **Alerting**: Alert on auto-disabled subscriptions or high failure rates

## Troubleshooting

### Webhooks Not Being Delivered

1. **Check Subscription Status**: Verify `is_active` is `true`
2. **Check Delivery Logs**: Look for error messages in `/deliveries` endpoint
3. **Verify URL**: Ensure webhook URL is accessible from public internet
4. **Check SSL Certificate**: Ensure valid SSL/TLS certificate (not self-signed)
5. **Review Firewall Rules**: Verify no firewall blocking incoming requests
6. **Test Endpoint**: Use `/test` endpoint to diagnose connectivity

### Auto-Disabled Subscription

If your subscription was auto-disabled due to consecutive failures:

1. **Review Delivery Logs**: Check error messages to identify root cause
2. **Fix Endpoint Issues**: Resolve any errors in your webhook endpoint
3. **Test Endpoint**: Use `/test` to verify fixes
4. **Re-enable**: Update subscription with `is_active: true`

### Invalid Signature Errors

If your signature verification is failing:

1. **Check Secret**: Ensure you're using the correct secret
2. **Verify Algorithm**: Ensure you're using HMAC-SHA256
3. **Check Message Format**: Verify `{timestamp}.{payload}` format
4. **Compare Hex**: Ensure hex encoding is lowercase
5. **Timing Safe Compare**: Use constant-time comparison to prevent timing attacks

## Configuration

Default configuration values (can be overridden in appsettings.json):

```json
{
  "Webhooks": {
    "BackgroundProcessor": {
      "IntervalSeconds": 5,
      "BatchSize": 20
    },
    "Client": {
      "MaxRetryAttempts": 5,
      "InitialRetryDelaySeconds": 2,
      "MaxSubscriptionsPerUser": 5
    }
  }
}
```

## Rate Limits

- **Subscriptions per User**: 5 active subscriptions maximum
- **Delivery Timeout**: 30 seconds per attempt
- **Background Processor**: Processes 20 deliveries every 5 seconds

## Support

For additional help:
- Review delivery logs for error details
- Check that your endpoint returns HTTP 2xx within 30 seconds
- Verify HMAC signature implementation matches examples
- Ensure HTTPS endpoint has valid SSL certificate
