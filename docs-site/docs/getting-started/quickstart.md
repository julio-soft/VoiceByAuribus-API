---
sidebar_position: 1
---

# Quickstart Guide

Get started with VoiceByAuribus API and complete your first voice conversion in minutes.

## Overview

This guide walks you through the complete workflow of converting audio using the VoiceByAuribus API:

1. **Authenticate**: Obtain an access token
2. **Discover**: Browse available voice models
3. **Upload**: Upload your audio file securely
4. **Convert**: Create a voice conversion job
5. **Retrieve**: Download your converted audio

## Prerequisites

Before you begin, you'll need:

- **API Credentials**: Client ID and Client Secret from Auribus
  - Contact [support@auribus.io](mailto:support@auribus.io) to get your credentials
- **HTTP Client**: curl, Postman, or your preferred programming language
- **Audio File**: A WAV, MP3, or FLAC file to convert (under 100MB)

## Step 1: Authenticate

VoiceByAuribus uses OAuth 2.0 Client Credentials for authentication. Exchange your credentials for an access token:

```bash
curl -X POST https://auth.auribus.io/oauth2/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=YOUR_CLIENT_ID" \
  -d "client_secret=YOUR_CLIENT_SECRET" \
  -d "scope=voicebyauribus"
```

**Response**:
```json
{
  "access_token": "eyJraWQiOiI...",
  "expires_in": 3600,
  "token_type": "Bearer"
}
```

:::tip Save Your Token
Access tokens expire after 1 hour. Save the token to use in all subsequent API requests. When it expires, request a new one using the same process.
:::

**Store the token for convenience**:
```bash
export TOKEN="eyJraWQiOiI..."
```

## Step 2: Discover Voice Models

Browse the available voice models you can use for conversions:

```bash
curl -X GET https://api.auribus.io/api/v1/voices \
  -H "Authorization: Bearer $TOKEN"
```

**Response**:
```json
{
  "success": true,
  "data": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "name": "Professional Female Voice",
      "description": "Clear and professional female voice, ideal for narration and presentations",
      "created_at": "2025-01-15T10:30:00Z"
    },
    {
      "id": "660e8400-e29b-41d4-a716-446655440001",
      "name": "Deep Male Voice",
      "description": "Rich, authoritative male voice perfect for voiceovers and announcements",
      "created_at": "2025-01-15T10:30:00Z"
    }
  ]
}
```

**Save a voice model ID**:
```bash
export VOICE_ID="550e8400-e29b-41d4-a716-446655440000"
```

## Step 3: Upload Audio File

Audio uploads use a secure two-step process with pre-signed URLs.

### Step 3a: Request Upload URL

First, request a secure upload URL from the API:

```bash
curl -X POST https://api.auribus.io/api/v1/audio-files \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "file_name": "my-audio.wav",
    "content_type": "audio/wav"
  }'
```

**Response**:
```json
{
  "success": true,
  "data": {
    "id": "770e8400-e29b-41d4-a716-446655440002",
    "upload_url": "https://storage.auribus.io/upload/...",
    "expires_at": "2025-01-15T10:50:00Z"
  }
}
```

**Save the audio file ID and upload URL**:
```bash
export AUDIO_ID="770e8400-e29b-41d4-a716-446655440002"
export UPLOAD_URL="https://storage.auribus.io/upload/..."
```

:::warning Upload URL Expires
The upload URL expires after **15 minutes**. Complete the upload before it expires or request a new URL.
:::

### Step 3b: Upload File to Storage

Upload your audio file directly to the pre-signed URL:

```bash
curl -X PUT "$UPLOAD_URL" \
  -H "Content-Type: audio/wav" \
  --upload-file my-audio.wav
```

**Success Response**: HTTP 200 with empty body

:::tip Processing Starts Automatically
Once uploaded, the system automatically processes your audio file. You don't need to wait for processing to complete before creating a conversion - the system handles that automatically!
:::

## Step 4: Create Voice Conversion

Create a conversion job to transform your audio using a voice model:

```bash
curl -X POST https://api.auribus.io/api/v1/voice-conversions \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "audio_file_id": "'"$AUDIO_ID"'",
    "voice_model_id": "'"$VOICE_ID"'",
    "pitch_shift": "same_octave",
    "use_preview": false
  }'
```

**Response**:
```json
{
  "success": true,
  "data": {
    "id": "880e8400-e29b-41d4-a716-446655440003",
    "audio_file_id": "770e8400-e29b-41d4-a716-446655440002",
    "voice_model_id": "550e8400-e29b-41d4-a716-446655440000",
    "pitch_shift": "same_octave",
    "use_preview": false,
    "status": "pending",
    "output_url": null,
    "created_at": "2025-01-15T10:40:00Z",
    "completed_at": null
  }
}
```

**Save the conversion ID**:
```bash
export CONVERSION_ID="880e8400-e29b-41d4-a716-446655440003"
```

### Pitch Shifting Options

You can adjust the pitch of the converted audio:

| Option | Description |
|--------|-------------|
| `same_octave` | No pitch change (default) |
| `lower_octave` | Shift down one octave |
| `higher_octave` | Shift up one octave |
| `third_down` | Shift down by a musical third |
| `third_up` | Shift up by a musical third |
| `fifth_down` | Shift down by a musical fifth |
| `fifth_up` | Shift up by a musical fifth |

## Step 5: Monitor Conversion Status

### Option A: Polling (Simple but Less Efficient)

Check the conversion status by polling the API:

```bash
curl -X GET https://api.auribus.io/api/v1/voice-conversions/$CONVERSION_ID \
  -H "Authorization: Bearer $TOKEN"
```

**Response (Processing)**:
```json
{
  "success": true,
  "data": {
    "id": "880e8400-e29b-41d4-a716-446655440003",
    "use_preview": false,
    "status": "processing",
    "output_url": null
  }
}
```

**Response (Completed)**:
```json
{
  "success": true,
  "data": {
    "id": "880e8400-e29b-41d4-a716-446655440003",
    "use_preview": false,
    "status": "completed",
    "output_url": "https://storage.auribus.io/output/...",
    "completed_at": "2025-01-15T10:45:00Z"
  }
}
```

### Option B: Webhooks (Recommended for Production)

Instead of polling, set up a webhook to receive instant notifications when conversions complete:

```bash
curl -X POST https://api.auribus.io/api/v1/webhook-subscriptions \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "url": "https://your-app.com/webhooks/voice-conversions",
    "events": ["conversion.completed", "conversion.failed"]
  }'
```

:::tip Use Webhooks for Production
Webhooks are more efficient than polling and provide instant notifications. See our [Webhook Guide](../guides/webhooks) for complete integration instructions.
:::

## Step 6: Download Converted Audio

Once the conversion is complete, download your converted audio file using the URL from Step 5:

```bash
curl -X GET "$OUTPUT_URL" \
  --output converted-audio.wav
```

:::info Download URL Validity
Download URLs are valid for **12 hours**. If a URL expires, call `GET /api/v1/voice-conversions/{id}` to get a fresh URL.
:::

:::tip Preview Mode
If you want a faster conversion for testing, use `"use_preview": true` when creating the conversion. This processes only a 10-second sample instead of the full audio.
:::

## Conversion Status Flow

Your conversion progresses through these states:

```
pending → processing → completed
                    ↓
                  failed
```

| Status | Description |
|--------|-------------|
| `pending` | Conversion is queued and waiting to start |
| `processing` | Conversion is actively being processed |
| `completed` | Conversion finished successfully - download URLs available |
| `failed` | Conversion failed - contact support if this occurs |

## Complete Example Script

Here's a complete bash script that performs the entire workflow:

```bash
#!/bin/bash

# Configuration
CLIENT_ID="your_client_id"
CLIENT_SECRET="your_client_secret"
AUDIO_FILE="my-audio.wav"
VOICE_MODEL_ID="550e8400-e29b-41d4-a716-446655440000"

# Step 1: Get access token
echo "Step 1: Authenticating..."
TOKEN_RESPONSE=$(curl -s -X POST https://auth.auribus.io/oauth2/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=$CLIENT_ID" \
  -d "client_secret=$CLIENT_SECRET" \
  -d "scope=voicebyauribus")

TOKEN=$(echo $TOKEN_RESPONSE | jq -r '.access_token')
echo "✓ Access token obtained"

# Step 2: Request upload URL
echo ""
echo "Step 2: Requesting upload URL..."
UPLOAD_RESPONSE=$(curl -s -X POST https://api.auribus.io/api/v1/audio-files \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"file_name\": \"$AUDIO_FILE\",
    \"content_type\": \"audio/wav\"
  }")

AUDIO_ID=$(echo $UPLOAD_RESPONSE | jq -r '.data.id')
UPLOAD_URL=$(echo $UPLOAD_RESPONSE | jq -r '.data.upload_url')
echo "✓ Upload URL obtained (Audio ID: $AUDIO_ID)"

# Step 3: Upload file
echo ""
echo "Step 3: Uploading audio file..."
curl -s -X PUT "$UPLOAD_URL" \
  -H "Content-Type: audio/wav" \
  --upload-file "$AUDIO_FILE"
echo "✓ Audio file uploaded"

# Step 4: Create conversion
echo ""
echo "Step 4: Creating voice conversion..."
CONVERSION_RESPONSE=$(curl -s -X POST https://api.auribus.io/api/v1/voice-conversions \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"audio_file_id\": \"$AUDIO_ID\",
    \"voice_model_id\": \"$VOICE_MODEL_ID\",
    \"pitch_shift\": \"same_octave\"
  }")

CONVERSION_ID=$(echo $CONVERSION_RESPONSE | jq -r '.data.id')
echo "✓ Conversion created (ID: $CONVERSION_ID)"

# Step 5: Poll for completion
echo ""
echo "Step 5: Waiting for conversion to complete..."
STATUS="pending"
while [ "$STATUS" != "completed" ] && [ "$STATUS" != "failed" ]; do
  sleep 5
  STATUS_RESPONSE=$(curl -s -X GET https://api.auribus.io/api/v1/voice-conversions/$CONVERSION_ID \
    -H "Authorization: Bearer $TOKEN")

  STATUS=$(echo $STATUS_RESPONSE | jq -r '.data.status')
  echo "   Status: $STATUS"
done

if [ "$STATUS" == "completed" ]; then
  echo "✓ Conversion completed!"

  # Step 6: Download converted audio
  echo ""
  echo "Step 6: Downloading converted audio..."
  OUTPUT_URL=$(echo $STATUS_RESPONSE | jq -r '.data.output_url')

  curl -s -X GET "$OUTPUT_URL" --output "converted-$AUDIO_FILE"
  echo "✓ Downloaded to: converted-$AUDIO_FILE"
else
  echo "✗ Conversion failed"
  exit 1
fi
```

**Make it executable and run**:
```bash
chmod +x voice-conversion.sh
./voice-conversion.sh
```

## Programming Language Examples

### Node.js / TypeScript

```typescript
import fetch from 'node-fetch';
import fs from 'fs';

const CLIENT_ID = 'your_client_id';
const CLIENT_SECRET = 'your_client_secret';
const AUDIO_FILE = 'my-audio.wav';
const VOICE_MODEL_ID = '550e8400-e29b-41d4-a716-446655440000';

async function convertVoice() {
  // Step 1: Authenticate
  console.log('Step 1: Authenticating...');
  const authResponse = await fetch('https://auth.auribus.io/oauth2/token', {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'client_credentials',
      client_id: CLIENT_ID,
      client_secret: CLIENT_SECRET,
      scope: 'voicebyauribus',
    }),
  });
  const { access_token } = await authResponse.json();
  console.log('✓ Access token obtained');

  // Step 2: Request upload URL
  console.log('\nStep 2: Requesting upload URL...');
  const uploadResponse = await fetch('https://api.auribus.io/api/v1/audio-files', {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${access_token}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      file_name: AUDIO_FILE,
      content_type: 'audio/wav',
    }),
  });
  const { data: uploadData } = await uploadResponse.json();
  console.log(`✓ Upload URL obtained (Audio ID: ${uploadData.id})`);

  // Step 3: Upload file
  console.log('\nStep 3: Uploading audio file...');
  const audioBuffer = fs.readFileSync(AUDIO_FILE);
  await fetch(uploadData.upload_url, {
    method: 'PUT',
    headers: { 'Content-Type': 'audio/wav' },
    body: audioBuffer,
  });
  console.log('✓ Audio file uploaded');

  // Step 4: Create conversion
  console.log('\nStep 4: Creating voice conversion...');
  const conversionResponse = await fetch('https://api.auribus.io/api/v1/voice-conversions', {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${access_token}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      audio_file_id: uploadData.id,
      voice_model_id: VOICE_MODEL_ID,
      pitch_shift: 'same_octave',
    }),
  });
  const { data: conversion } = await conversionResponse.json();
  console.log(`✓ Conversion created (ID: ${conversion.id})`);

  // Step 5: Poll for completion
  console.log('\nStep 5: Waiting for conversion to complete...');
  let status = 'pending';
  while (status !== 'completed' && status !== 'failed') {
    await new Promise(resolve => setTimeout(resolve, 5000));

    const statusResponse = await fetch(
      `https://api.auribus.io/api/v1/voice-conversions/${conversion.id}`,
      { headers: { 'Authorization': `Bearer ${access_token}` } }
    );
    const { data } = await statusResponse.json();
    status = data.status;
    console.log(`   Status: ${status}`);

    if (status === 'completed') {
      console.log('✓ Conversion completed!');

      // Step 6: Download converted audio
      console.log('\nStep 6: Downloading converted audio...');
      const audioResponse = await fetch(data.output_url);
      const audioData = await audioResponse.arrayBuffer();
      fs.writeFileSync(`converted-${AUDIO_FILE}`, Buffer.from(audioData));
      console.log(`✓ Downloaded to: converted-${AUDIO_FILE}`);
    }
  }
}

convertVoice().catch(console.error);
```

### Python

```python
import requests
import time
import os

CLIENT_ID = 'your_client_id'
CLIENT_SECRET = 'your_client_secret'
AUDIO_FILE = 'my-audio.wav'
VOICE_MODEL_ID = '550e8400-e29b-41d4-a716-446655440000'

def convert_voice():
    # Step 1: Authenticate
    print('Step 1: Authenticating...')
    auth_response = requests.post(
        'https://auth.auribus.io/oauth2/token',
        headers={'Content-Type': 'application/x-www-form-urlencoded'},
        data={
            'grant_type': 'client_credentials',
            'client_id': CLIENT_ID,
            'client_secret': CLIENT_SECRET,
            'scope': 'voicebyauribus',
        }
    )
    token = auth_response.json()['access_token']
    print('✓ Access token obtained')

    # Step 2: Request upload URL
    print('\nStep 2: Requesting upload URL...')
    upload_response = requests.post(
        'https://api.auribus.io/api/v1/audio-files',
        headers={
            'Authorization': f'Bearer {token}',
            'Content-Type': 'application/json',
        },
        json={
            'file_name': AUDIO_FILE,
            'content_type': 'audio/wav',
        }
    )
    upload_data = upload_response.json()['data']
    print(f"✓ Upload URL obtained (Audio ID: {upload_data['id']})")

    # Step 3: Upload file
    print('\nStep 3: Uploading audio file...')
    with open(AUDIO_FILE, 'rb') as f:
        requests.put(
            upload_data['upload_url'],
            headers={'Content-Type': 'audio/wav'},
            data=f
        )
    print('✓ Audio file uploaded')

    # Step 4: Create conversion
    print('\nStep 4: Creating voice conversion...')
    conversion_response = requests.post(
        'https://api.auribus.io/api/v1/voice-conversions',
        headers={
            'Authorization': f'Bearer {token}',
            'Content-Type': 'application/json',
        },
        json={
            'audio_file_id': upload_data['id'],
            'voice_model_id': VOICE_MODEL_ID,
            'pitch_shift': 'same_octave',
        }
    )
    conversion = conversion_response.json()['data']
    print(f"✓ Conversion created (ID: {conversion['id']})")

    # Step 5: Poll for completion
    print('\nStep 5: Waiting for conversion to complete...')
    status = 'pending'
    while status not in ('completed', 'failed'):
        time.sleep(5)

        status_response = requests.get(
            f"https://api.auribus.io/api/v1/voice-conversions/{conversion['id']}",
            headers={'Authorization': f'Bearer {token}'}
        )
        data = status_response.json()['data']
        status = data['status']
        print(f"   Status: {status}")

        if status == 'completed':
            print('✓ Conversion completed!')

            # Step 6: Download converted audio
            print('\nStep 6: Downloading converted audio...')
            audio_response = requests.get(data['output_url'])
            with open(f'converted-{AUDIO_FILE}', 'wb') as f:
                f.write(audio_response.content)
            print(f'✓ Downloaded to: converted-{AUDIO_FILE}')

if __name__ == '__main__':
    convert_voice()
```

## Best Practices

### 1. Token Management

- **Cache tokens**: Store tokens in memory and reuse them until they expire
- **Refresh before expiry**: Request a new token slightly before the 1-hour expiration
- **Secure storage**: Never hardcode credentials in your code; use environment variables

### 2. Error Handling

Always check HTTP status codes and handle errors appropriately:

```typescript
const response = await fetch(url, options);

if (!response.ok) {
  const error = await response.json();
  throw new Error(`API Error: ${error.message}`);
}
```

### 3. File Upload

- **Validate files**: Check file size (max 100MB) and format before uploading
- **Handle timeouts**: Upload URLs expire in 15 minutes; implement retry logic
- **Use streaming**: For large files, use streaming uploads to avoid memory issues

### 4. Conversion Monitoring

- **Use webhooks**: Prefer webhooks over polling for production applications
- **Implement retries**: Handle transient failures with exponential backoff
- **Set timeouts**: Don't poll indefinitely; set reasonable timeouts

### 5. Download URLs

- **Don't store URLs**: Pre-signed download URLs expire after 12 hours
- **Request fresh URLs**: Call the API to get new URLs if they expire
- **Download promptly**: Download files soon after conversion completes

## Troubleshooting

### Authentication Fails (401 Unauthorized)

- Verify your Client ID and Client Secret are correct
- Check that you're using the correct scope (`voicebyauribus`)
- Ensure you're including the `Bearer` token in the `Authorization` header

### Upload URL Expired

- Upload URLs expire after 15 minutes
- Request a new upload URL if yours has expired
- Implement retry logic to handle expiration automatically

### Conversion Stuck in Processing

- Large files take longer to process (processing time varies by file size)
- Use webhooks to receive notifications instead of polling
- Contact support if a conversion is processing for more than 30 minutes

### Download URL Returns 403 Forbidden

- Download URLs expire after 12 hours
- Call `GET /api/v1/voice-conversions/{id}` to get fresh URLs
- Don't store download URLs in your database

## Rate Limits

The API enforces the following rate limits:

- **Authentication**: 10 requests per minute
- **API Calls**: 100 requests per minute
- **File Uploads**: 20 uploads per minute

:::tip Optimize Performance
Use webhooks instead of polling to stay well within rate limits and receive instant notifications.
:::

## Next Steps

Now that you've completed your first voice conversion, explore these resources:

- **[Authentication Guide](authentication)**: Deep dive into OAuth 2.0 authentication
- **[Uploading Audio](../guides/uploading-audio)**: Detailed guide on audio file uploads
- **[Voice Conversion](../guides/voice-conversion)**: Learn about pitch shifting and advanced options
- **[Webhook Notifications](../guides/webhooks)**: Set up real-time notifications
- **[API Reference](../api/voicebyauribus-api)**: Complete API documentation with all endpoints

## Getting Help

Need assistance? We're here to help:

- **Email**: [support@auribus.io](mailto:support@auribus.io)
- **Documentation**: Browse our comprehensive guides
- **API Reference**: Check the interactive API documentation

Welcome to VoiceByAuribus! We're excited to see what you build.
