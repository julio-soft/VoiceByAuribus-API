---
sidebar_position: 2
---

# Webhook Signature Verification

Securely verify webhook requests using HMAC-SHA256 signatures.

## Overview

All webhook notifications from VoiceByAuribus are signed with HMAC-SHA256 to ensure they originated from our servers and haven't been tampered with. **You must verify signatures** to prevent unauthorized access to your webhook endpoint.

## Why Signature Verification Matters

Without signature verification, attackers could:
- Send fake webhook notifications to your endpoint
- Trigger unauthorized actions in your system
- Access sensitive business logic
- Cause data inconsistencies

:::danger Critical Security Requirement
**Always verify webhook signatures.** Never process webhooks without verification.
:::

## How Signatures Work

1. VoiceByAuribus computes HMAC-SHA256 of the raw request body using your webhook secret
2. The signature is sent in the `X-Webhook-Signature` header as `sha256=<hex-signature>`
3. Your server computes the same HMAC-SHA256 using the raw body and your secret
4. Compare the computed signature with the received signature
5. Process the webhook only if signatures match

## Signature Header Format

```
X-Webhook-Signature: sha256=a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0u1v2w3x4y5z6a7b8c9d0e1f2
```

The value is always prefixed with `sha256=` followed by the hexadecimal signature.

## Implementation

### Node.js / TypeScript

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

  // Use constant-time comparison to prevent timing attacks
  return crypto.timingSafeEqual(
    Buffer.from(computedSignature),
    Buffer.from(receivedSignature)
  );
}

// Express.js middleware
import express from 'express';

app.post(
  '/webhooks/voice-conversions',
  express.raw({ type: 'application/json' }), // Important: Get raw body
  (req, res) => {
    const signature = req.headers['x-webhook-signature'] as string;
    const rawBody = req.body.toString('utf8');
    const secret = process.env.WEBHOOK_SECRET!;

    if (!signature) {
      return res.status(401).send('Missing signature');
    }

    if (!verifyWebhookSignature(rawBody, signature, secret)) {
      return res.status(401).send('Invalid signature');
    }

    // Signature verified - safe to process
    const event = JSON.parse(rawBody);
    // Process event...

    res.status(200).send('OK');
  }
);
```

### Python

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

    # Constant-time comparison to prevent timing attacks
    return hmac.compare_digest(computed_signature, received_signature)

# Flask example
from flask import Flask, request, jsonify

@app.route('/webhooks/voice-conversions', methods=['POST'])
def webhook():
    signature = request.headers.get('X-Webhook-Signature')
    raw_body = request.get_data()
    secret = os.environ['WEBHOOK_SECRET']

    if not signature:
        return jsonify({'error': 'Missing signature'}), 401

    if not verify_webhook_signature(raw_body, signature, secret):
        return jsonify({'error': 'Invalid signature'}), 401

    # Signature verified - safe to process
    event = request.get_json()
    # Process event...

    return '', 200
```

### C# / .NET

```csharp
using System.Security.Cryptography;
using System.Text;

public class WebhookSignatureValidator
{
    public static bool VerifySignature(string rawBody, string signature, string secret)
    {
        // Remove 'sha256=' prefix
        var receivedSignature = signature.Replace("sha256=", "");

        // Compute HMAC-SHA256
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        var computedSignature = BitConverter.ToString(hash).Replace("-", "").ToLower();

        // Constant-time comparison to prevent timing attacks
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedSignature),
            Encoding.UTF8.GetBytes(receivedSignature)
        );
    }
}

// ASP.NET Core controller
[HttpPost("webhooks/voice-conversions")]
public async Task<IActionResult> ReceiveWebhook()
{
    using var reader = new StreamReader(Request.Body);
    var rawBody = await reader.ReadToEndAsync();

    var signature = Request.Headers["X-Webhook-Signature"].ToString();
    var secret = Configuration["WebhookSecret"];

    if (string.IsNullOrEmpty(signature))
    {
        return Unauthorized(new { error = "Missing signature" });
    }

    if (!WebhookSignatureValidator.VerifySignature(rawBody, signature, secret))
    {
        return Unauthorized(new { error = "Invalid signature" });
    }

    // Signature verified - safe to process
    var webhookEvent = JsonSerializer.Deserialize<WebhookEvent>(rawBody);
    // Process event...

    return Ok();
}
```

### PHP

```php
<?php

function verify_webhook_signature(string $raw_body, string $signature, string $secret): bool {
    // Remove 'sha256=' prefix
    $received_signature = str_replace('sha256=', '', $signature);

    // Compute HMAC-SHA256
    $computed_signature = hash_hmac('sha256', $raw_body, $secret);

    // Constant-time comparison to prevent timing attacks
    return hash_equals($computed_signature, $received_signature);
}

// Example usage
$raw_body = file_get_contents('php://input');
$signature = $_SERVER['HTTP_X_WEBHOOK_SIGNATURE'] ?? '';
$secret = getenv('WEBHOOK_SECRET');

if (empty($signature)) {
    http_response_code(401);
    echo json_encode(['error' => 'Missing signature']);
    exit;
}

if (!verify_webhook_signature($raw_body, $signature, $secret)) {
    http_response_code(401);
    echo json_encode(['error' => 'Invalid signature']);
    exit;
}

// Signature verified - safe to process
$event = json_decode($raw_body, true);
// Process event...

http_response_code(200);
echo 'OK';
```

## Security Best Practices

### 1. Use Constant-Time Comparison

**Always use constant-time comparison** to prevent timing attacks:

```typescript
// Good: Constant-time comparison
crypto.timingSafeEqual(Buffer.from(sig1), Buffer.from(sig2));

// Bad: Simple string comparison (vulnerable to timing attacks)
if (sig1 === sig2) { /* DON'T DO THIS */ }
```

Timing attacks analyze response time differences to guess the correct signature.

### 2. Verify Before Parsing

Verify the signature **before** parsing the JSON body:

```typescript
// Good: Verify first
if (!verifySignature(rawBody, signature, secret)) {
  return res.status(401).send('Invalid signature');
}
const event = JSON.parse(rawBody); // Now safe to parse

// Bad: Parse before verifying
const event = JSON.parse(rawBody); // DON'T DO THIS
if (!verifySignature(...)) { return; }
```

### 3. Use Raw Request Body

The signature is computed on the **raw request body**, not the parsed JSON:

```typescript
// Good: Use raw body middleware
app.post('/webhook', express.raw({ type: 'application/json' }), handler);

// Bad: JSON middleware modifies body
app.post('/webhook', express.json(), handler); // DON'T DO THIS
```

### 4. Protect Your Secret

The webhook secret is sensitive - treat it like a password:

```typescript
// Good: Environment variable
const secret = process.env.WEBHOOK_SECRET;

// Bad: Hardcoded secret
const secret = 'whsec_abc123...'; // DON'T DO THIS
```

### 5. Handle Missing Signatures

Reject requests without signatures:

```typescript
if (!signature) {
  logger.warn('Webhook request without signature', {
    ip: req.ip,
    timestamp: Date.now(),
  });
  return res.status(401).send('Missing signature');
}
```

### 6. Log Verification Failures

Monitor failed verification attempts:

```typescript
if (!verifySignature(rawBody, signature, secret)) {
  logger.error('Webhook signature verification failed', {
    ip: req.ip,
    receivedSignature: signature.substring(0, 16) + '...', // Don't log full signature
    timestamp: Date.now(),
  });
  return res.status(401).send('Invalid signature');
}
```

## Common Mistakes

### Mistake 1: Parsing Body Before Verification

```typescript
// WRONG: Body is parsed/modified before verification
app.use(express.json()); // Parses all JSON bodies
app.post('/webhook', (req, res) => {
  const signature = req.headers['x-webhook-signature'];
  // req.body is already parsed - signature won't match!
  verifySignature(JSON.stringify(req.body), signature, secret); // Will fail
});

// CORRECT: Use raw body
app.post(
  '/webhook',
  express.raw({ type: 'application/json' }),
  (req, res) => {
    const signature = req.headers['x-webhook-signature'];
    const rawBody = req.body.toString('utf8');
    verifySignature(rawBody, signature, secret); // Works correctly
  }
);
```

### Mistake 2: Simple String Comparison

```typescript
// WRONG: Vulnerable to timing attacks
if (computedSignature === receivedSignature) { /* DON'T */ }

// CORRECT: Constant-time comparison
crypto.timingSafeEqual(
  Buffer.from(computedSignature),
  Buffer.from(receivedSignature)
);
```

### Mistake 3: Not Removing Prefix

```typescript
// WRONG: Comparing with prefix
const signature = 'sha256=abc123...';
verifySignature(rawBody, signature, secret); // Will fail

// CORRECT: Remove prefix first
const signature = req.headers['x-webhook-signature'].replace('sha256=', '');
verifySignature(rawBody, signature, secret); // Works
```

### Mistake 4: Wrong Encoding

```typescript
// WRONG: Using wrong encoding
const rawBody = req.body.toString('base64'); // Wrong!

// CORRECT: UTF-8 encoding
const rawBody = req.body.toString('utf8'); // Correct
```

## Regenerating Secrets

If your webhook secret is compromised:

1. **Regenerate the secret**:
   ```bash
   curl -X POST https://api.auribus.io/api/v1/webhook-subscriptions/{id}/regenerate-secret \
     -H "Authorization: Bearer $TOKEN"
   ```

2. **Update your application** with the new secret

3. **Test** webhook verification

4. **Monitor** for verification failures

:::warning Secret Shown Once
The new secret is shown **only once** in the response. Save it immediately.
:::

## Testing Signature Verification

### Generate Test Signature

```typescript
// Generate a test webhook with valid signature
const testPayload = {
  event: 'conversion.completed',
  data: { id: 'test-123' },
};

const rawBody = JSON.stringify(testPayload);
const secret = process.env.WEBHOOK_SECRET;

const hmac = crypto.createHmac('sha256', secret);
hmac.update(rawBody);
const signature = `sha256=${hmac.digest('hex')}`;

// Send test request
await fetch('https://your-app.com/webhook', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'X-Webhook-Signature': signature,
  },
  body: rawBody,
});
```

### Test Invalid Signatures

```typescript
// Test that invalid signatures are rejected
const invalidSignature = 'sha256=invalid';

const response = await fetch('https://your-app.com/webhook', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'X-Webhook-Signature': invalidSignature,
  },
  body: JSON.stringify(testPayload),
});

// Should return 401 Unauthorized
expect(response.status).toBe(401);
```

## Security Checklist

- [ ] Signature verification implemented
- [ ] Using constant-time comparison
- [ ] Verifying before parsing body
- [ ] Using raw request body
- [ ] Secret stored securely (environment variable)
- [ ] Rejecting requests without signatures
- [ ] Logging verification failures
- [ ] Testing with valid and invalid signatures
- [ ] Have secret rotation procedure documented

## Next Steps

- [Webhook Integration Guide](../guides/webhooks)
- [Authentication Security](authentication)
- [Error Handling](../guides/error-handling)

## Getting Help

Security questions about webhooks? Contact us:

- **Email**: [support@auribus.io](mailto:support@auribus.io)
- **Security Issues**: Mark with "SECURITY:" prefix
