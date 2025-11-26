---
sidebar_position: 3
---

# Voice Conversion

Transform your audio files using professional voice models with the VoiceByAuribus API.

## Overview

Voice conversion is the process of transforming source audio to match the characteristics of a selected voice model while preserving the original content, timing, and emotional expression. VoiceByAuribus provides high-quality voice conversion with pitch shifting capabilities.

### How It Works

1. **Upload** your source audio file
2. **Select** a voice model from our library
3. **Configure** pitch shifting (optional)
4. **Process** the conversion job
5. **Download** your converted audio

The entire process is handled asynchronously, allowing you to submit multiple conversion jobs and receive notifications when they complete.

## Getting Available Voice Models

Before creating a conversion, you need to select a voice model. The API provides two endpoints to explore voice models:

### List All Voice Models

Retrieve the complete list of available voices:

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
      "name": "Sarah Mitchell",
      "tags": ["female", "professional", "narration", "clear"],
      "image_url": "https://storage.auribus.io/voices/sarah-mitchell/image.jpg",
      "song_url": "https://storage.auribus.io/voices/sarah-mitchell/preview.mp3"
    },
    {
      "id": "660e8400-e29b-41d4-a716-446655440001",
      "name": "Marcus Johnson",
      "tags": ["male", "deep", "authoritative", "announcer"],
      "image_url": "https://storage.auribus.io/voices/marcus-johnson/image.jpg",
      "song_url": "https://storage.auribus.io/voices/marcus-johnson/preview.mp3"
    }
  ]
}
```

**Response Fields**:
- **id**: Unique identifier for the voice model (use this in conversion requests)
- **name**: Display name of the voice model
- **tags**: Descriptive tags for filtering and categorization (e.g., gender, style, use case)
- **image_url**: URL to voice model profile image
- **song_url**: URL to audio preview sample - play this to hear the voice singing

### Get Voice Model Details

Retrieve detailed information about a specific voice model:

```bash
curl -X GET https://api.auribus.io/api/v1/voices/{id} \
  -H "Authorization: Bearer $TOKEN"
```

**Example**:
```bash
curl -X GET https://api.auribus.io/api/v1/voices/550e8400-e29b-41d4-a716-446655440000 \
  -H "Authorization: Bearer $TOKEN"
```

**Response**:
```json
{
  "success": true,
  "data": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "name": "Sarah Mitchell",
    "tags": ["female", "professional", "narration", "clear"],
    "image_url": "https://storage.auribus.io/voices/sarah-mitchell/image.jpg",
    "song_url": "https://storage.auribus.io/voices/sarah-mitchell/preview.mp3"
  }
}
```

### Previewing Voice Models

Before selecting a voice model for your conversion, **listen to the audio preview**:

1. Get the list of available voices
2. Note the `song_url` for voices you're interested in
3. Play the audio preview to hear the voice model singing
4. Copy the `id` of your chosen voice model for the conversion request

```bash
# Download and play a voice preview
curl -X GET "https://storage.auribus.io/voices/sarah-mitchell/preview.mp3" \
  --output sarah-preview.mp3
# Play with your audio player
```

:::tip Best Practice
Always listen to the `song_url` preview before creating conversions. This helps you select the voice that best matches your desired output style and quality.
:::

:::tip Exploring Voice Models
Use the [quickstart guide](../getting-started/quickstart#step-2-discover-voice-models) for a complete example of browsing and selecting voice models.
:::

## Creating a Conversion

### Basic Conversion

Create a voice conversion with default settings:

```bash
curl -X POST https://api.auribus.io/api/v1/voice-conversions \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "audio_file_id": "660e8400-e29b-41d4-a716-446655440001",
    "voice_model_id": "550e8400-e29b-41d4-a716-446655440000",
    "pitch_shift": "same_octave",
    "use_preview": false
  }'
```

**Response**:
```json
{
  "success": true,
  "data": {
    "id": "770e8400-e29b-41d4-a716-446655440002",
    "audio_file_id": "660e8400-e29b-41d4-a716-446655440001",
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

### Pitch Shifting Options

Adjust the pitch of the converted audio to match your desired vocal range. VoiceByAuribus provides seven pitch shifting options:

| Option | Semitones | Description |
|--------|-----------|-------------|
| `same_octave` | 0 | No pitch change (default) |
| `third_down` | -4 | Subtle deepening |
| `third_up` | +4 | Subtle brightening |
| `fifth_down` | -7 | Moderate lowering |
| `fifth_up` | +7 | Moderate raising |
| `lower_octave` | -12 | Dramatic deepening |
| `higher_octave` | +12 | Dramatic brightening |

**Example with pitch shift**:

```bash
curl -X POST https://api.auribus.io/api/v1/voice-conversions \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "audio_file_id": "660e8400-e29b-41d4-a716-446655440001",
    "voice_model_id": "550e8400-e29b-41d4-a716-446655440000",
    "pitch_shift": "fifth_up"
  }'
```

:::tip Choosing Pitch Shifts
Start with subtle adjustments (`third_up` or `third_down`) and increase incrementally. Thirds and fifths generally sound more natural than full octave shifts.
:::

## Conversion Lifecycle

### Status Flow

```
pending → processing → completed
                    ↓
                  failed
```

| Status | Description | Actions Available |
|--------|-------------|-------------------|
| `pending` | Queued, waiting for audio file preprocessing | Wait or poll status |
| `processing` | Actively converting audio | Wait or poll status |
| `completed` | Conversion finished successfully | Download output files |
| `failed` | Conversion failed | Contact support, retry |

### Checking Status

Poll the conversion status:

```bash
curl -X GET https://api.auribus.io/api/v1/voice-conversions/{id} \
  -H "Authorization: Bearer $TOKEN"
```

**Response (Completed)**:
```json
{
  "success": true,
  "data": {
    "id": "770e8400-e29b-41d4-a716-446655440002",
    "use_preview": false,
    "status": "completed",
    "output_url": "https://storage.auribus.io/output/...",
    "completed_at": "2025-01-15T10:45:00Z"
  }
}
```

:::tip Use Webhooks Instead
Instead of polling, use [webhook notifications](webhooks) to receive instant updates when conversions complete. This is more efficient and provides better user experience.
:::

## Output Files

Each completed conversion provides a converted audio file accessible via the `output_url` field.

### Understanding `use_preview`

When creating a conversion, the `use_preview` parameter determines which audio is converted:

- **`use_preview: false` (default)**: Converts the **full audio file**
- **`use_preview: true`**: Converts only a **10-second preview** from the beginning

Both options return a single `output_url` containing the converted audio.

### Downloading Converted Audio

```bash
# Get conversion status and download URL
RESPONSE=$(curl -X GET https://api.auribus.io/api/v1/voice-conversions/{id} \
  -H "Authorization: Bearer $TOKEN")

# Extract output URL
OUTPUT_URL=$(echo $RESPONSE | jq -r '.data.output_url')

# Download converted audio
curl -X GET "$OUTPUT_URL" --output converted-audio.wav
```

**Output URL Properties**:
- **Format**: Same as input (WAV, MP3, or FLAC)
- **Duration**: Full audio length if `use_preview: false`, or ~10 seconds if `use_preview: true`
- **Validity**: URL valid for 12 hours

:::info URL Expiration
Download URLs expire after **12 hours**. If a URL expires, call `GET /api/v1/voice-conversions/{id}` to get a fresh URL.
:::

### When to Use Preview Mode

Preview mode (`use_preview: true`) is useful for:
- **Quick testing**: Test voice models without processing the entire audio
- **Faster results**: Preview conversions complete much faster
- **Cost optimization**: Process only what you need to evaluate

```bash
# Create a preview conversion for testing
curl -X POST https://api.auribus.io/api/v1/voice-conversions \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type": application/json" \
  -d '{
    "audio_file_id": "660e8400-e29b-41d4-a716-446655440001",
    "voice_model_id": "550e8400-e29b-41d4-a716-446655440000",
    "pitch_shift": "same_octave",
    "use_preview": true
  }'
```

## Common Patterns

### Batch Processing

Convert multiple files with the same voice model by making individual API calls for each file:

:::info Note
The API processes conversions one at a time. To convert multiple files, make separate POST requests for each conversion. Consider using webhooks to handle completion notifications efficiently.
:::

```bash
# Array of audio file IDs
AUDIO_FILES=("file-id-1" "file-id-2" "file-id-3")
VOICE_MODEL="550e8400-e29b-41d4-a716-446655440000"

for AUDIO_ID in "${AUDIO_FILES[@]}"; do
  curl -X POST https://api.auribus.io/api/v1/voice-conversions \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d "{
      \"audio_file_id\": \"$AUDIO_ID\",
      \"voice_model_id\": \"$VOICE_MODEL\",
      \"pitch_shift\": \"same_octave\"
    }"

  echo "Created conversion for $AUDIO_ID"
done
```

### A/B Testing Voice Models

Compare different voice models on the same audio:

```bash
AUDIO_ID="660e8400-e29b-41d4-a716-446655440001"
VOICE_MODELS=("voice-id-1" "voice-id-2" "voice-id-3")

for VOICE_ID in "${VOICE_MODELS[@]}"; do
  curl -X POST https://api.auribus.io/api/v1/voice-conversions \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d "{
      \"audio_file_id\": \"$AUDIO_ID\",
      \"voice_model_id\": \"$VOICE_ID\",
      \"pitch_shift\": \"same_octave\"
    }"
done
```

### Pitch Variation Testing

Create multiple versions with different pitch shifts:

```bash
AUDIO_ID="660e8400-e29b-41d4-a716-446655440001"
VOICE_ID="550e8400-e29b-41d4-a716-446655440000"
PITCHES=("third_down" "same_octave" "third_up" "fifth_up")

for PITCH in "${PITCHES[@]}"; do
  curl -X POST https://api.auribus.io/api/v1/voice-conversions \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d "{
      \"audio_file_id\": \"$AUDIO_ID\",
      \"voice_model_id\": \"$VOICE_ID\",
      \"pitch_shift\": \"$PITCH\"
    }"

  echo "Created conversion with pitch: $PITCH"
done
```

## Programming Examples

### Node.js / TypeScript

```typescript
interface VoiceConversion {
  audio_file_id: string;
  voice_model_id: string;
  pitch_shift: string;
}

async function createConversion(conversion: VoiceConversion): Promise<any> {
  const response = await fetch('https://api.auribus.io/api/v1/voice-conversions', {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(conversion),
  });

  return await response.json();
}

async function pollConversionStatus(conversionId: string): Promise<any> {
  let status = 'pending';

  while (status !== 'completed' && status !== 'failed') {
    await new Promise(resolve => setTimeout(resolve, 5000)); // Wait 5 seconds

    const response = await fetch(
      `https://api.auribus.io/api/v1/voice-conversions/${conversionId}`,
      {
        headers: { 'Authorization': `Bearer ${token}` }
      }
    );

    const { data } = await response.json();
    status = data.status;

    console.log(`Status: ${status}`);
  }

  return status;
}

// Example usage
const conversion = await createConversion({
  audio_file_id: 'audio-id',
  voice_model_id: 'voice-id',
  pitch_shift: 'same_octave',
});

const finalStatus = await pollConversionStatus(conversion.data.id);

if (finalStatus === 'completed') {
  // Download files
  console.log('Conversion completed!');
}
```

### Python

```python
import requests
import time
from typing import Dict, Any

def create_conversion(
    audio_file_id: str,
    voice_model_id: str,
    pitch_shift: str,
    token: str
) -> Dict[str, Any]:
    """Create a voice conversion job."""
    response = requests.post(
        'https://api.auribus.io/api/v1/voice-conversions',
        headers={
            'Authorization': f'Bearer {token}',
            'Content-Type': 'application/json',
        },
        json={
            'audio_file_id': audio_file_id,
            'voice_model_id': voice_model_id,
            'pitch_shift': pitch_shift,
        }
    )
    return response.json()

def poll_conversion_status(conversion_id: str, token: str) -> str:
    """Poll conversion status until complete or failed."""
    status = 'pending'

    while status not in ('completed', 'failed'):
        time.sleep(5)  # Wait 5 seconds

        response = requests.get(
            f'https://api.auribus.io/api/v1/voice-conversions/{conversion_id}',
            headers={'Authorization': f'Bearer {token}'}
        )

        data = response.json()['data']
        status = data['status']

        print(f'Status: {status}')

    return status

# Example usage
conversion = create_conversion(
    audio_file_id='audio-id',
    voice_model_id='voice-id',
    pitch_shift='same_octave',
    token=token
)

conversion_id = conversion['data']['id']
final_status = poll_conversion_status(conversion_id, token)

if final_status == 'completed':
    print('Conversion completed!')
```

## Best Practices

### 1. Use Webhooks for Production

Instead of polling, implement [webhook notifications](webhooks) to receive instant updates:

```bash
# Create webhook subscription
curl -X POST https://api.auribus.io/api/v1/webhook-subscriptions \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "url": "https://your-app.com/webhooks/conversions",
    "events": ["conversion.completed", "conversion.failed"]
  }'
```

### 2. Start Conversions Immediately

You don't need to wait for audio file preprocessing to finish before creating a conversion:

```bash
# Step 1: Upload audio
UPLOAD_RESPONSE=$(curl -X POST https://api.auribus.io/api/v1/audio-files ...)
AUDIO_ID=$(echo $UPLOAD_RESPONSE | jq -r '.data.id')

# Step 2: Upload to S3
curl -X PUT "$UPLOAD_URL" --upload-file audio.wav

# Step 3: Create conversion immediately (no waiting!)
curl -X POST https://api.auribus.io/api/v1/voice-conversions \
  -d '{"audio_file_id": "'"$AUDIO_ID"'",...}'
```

The system automatically queues the conversion and starts processing when the audio file is ready.

### 3. Download Files Promptly

Download URLs expire after 12 hours:

- Download files soon after conversion completes
- Don't store download URLs in your database
- Request fresh URLs if needed by calling `GET /voice-conversions/{id}`

### 4. Handle Failures Gracefully

Implement proper error handling:

```typescript
const response = await fetch('https://api.auribus.io/api/v1/voice-conversions', options);

if (!response.ok) {
  const error = await response.json();
  console.error('Conversion failed:', error.message);
  // Implement retry logic or user notification
}
```

### 5. Test with Preview Mode

Use `use_preview: true` to quickly test voice models and settings before processing full audio:

```bash
# Create a preview conversion (10 seconds)
curl -X POST https://api.auribus.io/api/v1/voice-conversions \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type": application/json" \
  -d '{
    "audio_file_id": "'"$AUDIO_ID"'",
    "voice_model_id": "'"$VOICE_ID"'",
    "pitch_shift": "same_octave",
    "use_preview": true
  }'

# Listen to preview result, then create full conversion if satisfied
```

## Troubleshooting

### Conversion Stays in Pending

**Cause**: Audio file is still preprocessing.

**Solution**: Wait for audio file preprocessing to complete. This is automatic - the conversion will start when ready.

### Conversion Failed

**Causes**:
- Source audio file is corrupted
- Unsupported audio format
- Audio file too large (>100MB)
- System error

**Solution**: Check audio file quality and format. Contact support if issue persists.

### Download URL Returns 403 Forbidden

**Cause**: Download URL has expired (>12 hours old).

**Solution**:
```bash
# Get fresh URLs
curl -X GET https://api.auribus.io/api/v1/voice-conversions/{id} \
  -H "Authorization: Bearer $TOKEN"
```

### Conversion Sounds Unnatural

**Causes**:
- Pitch shift too extreme
- Voice model not suitable for source audio
- Low-quality source audio

**Solutions**:
1. Try a more subtle pitch shift (e.g., `third_up` or `third_down` instead of full octaves)
2. Test different voice models
3. Improve source audio quality

## Rate Limits

Conversion endpoints have the following limits:

- **Create Conversions**: 100 per minute
- **Get Status**: 100 per minute
- **List Conversions**: 100 per minute

:::tip Stay Within Limits
Use webhooks instead of frequent polling to stay well within rate limits.
:::

## Next Steps

- **[Webhook Notifications](webhooks)**: Set up real-time conversion notifications
- **[Uploading Audio](uploading-audio)**: Detailed guide on audio file uploads
- **[Quickstart Guide](../getting-started/quickstart)**: Complete end-to-end example
- **[API Reference](../api/voicebyauribus-api)**: Full API documentation

## Getting Help

Need assistance with voice conversions? We're here to help:

- **Email**: [support@auribus.io](mailto:support@auribus.io)
- **Technical Support**: Get help choosing voice models and optimizing conversions
