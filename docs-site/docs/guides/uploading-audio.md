---
sidebar_position: 1
---

# Uploading Audio Files

Learn how to securely upload audio files to VoiceByAuribus API.

## Overview

VoiceByAuribus uses a secure two-step upload process with pre-signed URLs. This ensures your audio files are uploaded directly to secure storage without passing through our servers.

## Supported Formats

- **WAV** (Recommended): Uncompressed, highest quality
- **MP3**: Compressed, good for smaller files
- **FLAC**: Lossless compression
- **MPEG**: General audio format

## Upload Process

### Step 1: Request Upload URL

Request a secure pre-signed URL from the API:

```bash
curl -X POST https://api.auribus.io/api/v1/audio-files \
  -H "Authorization: Bearer YOUR_TOKEN" \
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
    "id": "660e8400-e29b-41d4-a716-446655440001",
    "upload_url": "https://storage.auribus.io/upload/...",
    "expires_at": "2025-01-15T10:50:00Z"
  }
}
```

:::warning Important
The upload URL expires after **15 minutes**. Complete the upload before it expires.
:::

### Step 2: Upload File to Storage

Upload your audio file directly to the pre-signed URL:

```bash
curl -X PUT "UPLOAD_URL_FROM_STEP_1" \
  -H "Content-Type: audio/wav" \
  --upload-file my-audio.wav
```

### Step 3: Check Processing Status

Once uploaded, the system automatically processes your audio file to optimize it for conversions. You can check the status:

```bash
curl -X GET https://api.auribus.io/api/v1/audio-files/660e8400-e29b-41d4-a716-446655440001 \
  -H "Authorization: Bearer YOUR_TOKEN"
```

**Response**:
```json
{
  "success": true,
  "data": {
    "id": "660e8400-e29b-41d4-a716-446655440001",
    "file_name": "my-audio.wav",
    "status": "ready",
    "created_at": "2025-01-15T10:35:00Z"
  }
}
```

## Audio File Status

Your audio file will have one of the following statuses:

| Status | Description |
|--------|-------------|
| `pending` | File was uploaded and is queued for processing |
| `processing` | System is optimizing the audio for conversions |
| `ready` | Audio is ready to use in voice conversions |
| `failed` | Processing failed (contact support if this occurs) |

## Creating Conversions

You can create voice conversion jobs as soon as your audio file is uploaded. The system will automatically queue the conversion and start processing once the audio file is ready.

```bash
# You don't need to wait for 'ready' status!
curl -X POST https://api.auribus.io/api/v1/voice-conversions \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "audio_file_id": "660e8400-e29b-41d4-a716-446655440001",
    "voice_model_id": "550e8400-e29b-41d4-a716-446655440000",
    "pitch_shift": "same_octave"
  }'
```

:::tip Best Practice
Create conversion jobs immediately after upload. The system handles processing automatically and will start the conversion as soon as the audio is ready.
:::

## Complete Example

Here's a complete workflow from upload to conversion:

```bash
# 1. Request upload URL
RESPONSE=$(curl -X POST https://api.auribus.io/api/v1/audio-files \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "file_name": "podcast-episode.wav",
    "content_type": "audio/wav"
  }')

# 2. Extract upload URL and audio file ID
UPLOAD_URL=$(echo $RESPONSE | jq -r '.data.upload_url')
AUDIO_ID=$(echo $RESPONSE | jq -r '.data.id')

# 3. Upload the file
curl -X PUT "$UPLOAD_URL" \
  -H "Content-Type: audio/wav" \
  --upload-file podcast-episode.wav

# 4. Create conversion immediately
curl -X POST https://api.auribus.io/api/v1/voice-conversions \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"audio_file_id\": \"$AUDIO_ID\",
    \"voice_model_id\": \"550e8400-e29b-41d4-a716-446655440000\",
    \"pitch_shift\": \"same_octave\"
  }"
```

## Best Practices

1. **File Size**: Keep individual files under 100MB for optimal performance
2. **Format**: Use WAV format for highest quality results
3. **Naming**: Use descriptive filenames to easily identify your files
4. **Error Handling**: Always check the HTTP status code and handle errors appropriately
5. **Webhooks**: Configure webhooks to receive notifications when conversions complete instead of polling

## Next Steps

- [Learn about voice conversions](voice-conversion)
- [Configure webhook notifications](webhooks)
