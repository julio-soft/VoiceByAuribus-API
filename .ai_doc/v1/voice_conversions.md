# Voice Conversions API - v1

## Overview

The Voice Conversions feature allows users to convert audio files using AI voice models with configurable pitch transposition. The system handles asynchronous processing with automatic retry logic for pending conversions.

## Endpoints

### Create Voice Conversion

Creates a new voice conversion request.

**Request:**
```http
POST /api/v1/voice-conversions
Authorization: Bearer {token}
Content-Type: application/json

{
  "audio_file_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "voice_model_id": "550e8400-e29b-41d4-a716-446655440000",
  "transposition": "SameOctave"
}
```

**Transposition Options:**
- `SameOctave` (0 semitones)
- `LowerOctave` (-12 semitones)
- `HigherOctave` (+12 semitones)
- `ThirdDown` (-4 semitones)
- `ThirdUp` (+4 semitones)
- `FifthDown` (-7 semitones)
- `FifthUp` (+7 semitones)

**Response (201 Created):**
```json
{
  "success": true,
  "message": null,
  "data": {
    "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "audio_file_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "audio_file_name": "my-audio.mp3",
    "voice_model_id": "550e8400-e29b-41d4-a716-446655440000",
    "voice_model_name": "Artist Name",
    "transposition": "SameOctave",
    "transposition_value": 0,
    "status": "PendingPreprocessing",
    "output_url": null,
    "output_s3_uri": null,
    "created_at": "2025-11-17T20:30:00Z",
    "queued_at": null,
    "processing_started_at": null,
    "completed_at": null,
    "error_message": null,
    "retry_count": null
  },
  "errors": null
}
```

**Status Flow:**
1. **PendingPreprocessing**: Audio preprocessing not yet complete
2. **Queued**: Sent to SQS inference queue
3. **Processing**: External service processing the conversion
4. **Completed**: Conversion successful, output_url available
5. **Failed**: Conversion or preprocessing failed

**Business Rules:**
- Audio file must exist and belong to the authenticated user
- Voice model must exist
- If audio preprocessing has failed, request will be rejected
- If audio preprocessing is complete, conversion is queued immediately
- If audio preprocessing is pending/processing, conversion waits in PendingPreprocessing status
- Background processor (Lambda) checks pending conversions every 5 minutes

**Error Responses:**

400 Bad Request - Invalid input:
```json
{
  "success": false,
  "message": "Audio file not found: {id}",
  "data": null,
  "errors": null
}
```

400 Bad Request - Preprocessing failed:
```json
{
  "success": false,
  "message": "Audio file preprocessing failed: {error_details}",
  "data": null,
  "errors": null
}
```

---

### Get Voice Conversion

Retrieves a voice conversion by ID.

**Request:**
```http
GET /api/v1/voice-conversions/{id}
Authorization: Bearer {token}
```

**Response (200 OK):**
```json
{
  "success": true,
  "message": null,
  "data": {
    "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "audio_file_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "audio_file_name": "my-audio.mp3",
    "voice_model_id": "550e8400-e29b-41d4-a716-446655440000",
    "voice_model_name": "Artist Name",
    "transposition": "SameOctave",
    "transposition_value": 0,
    "status": "Completed",
    "output_url": "https://voice-by-auribus-api.s3.amazonaws.com/...",
    "output_s3_uri": "s3://voice-by-auribus-api/audio-files/{user_id}/converted/{file_id}.mp3",
    "created_at": "2025-11-17T20:30:00Z",
    "queued_at": "2025-11-17T20:30:05Z",
    "processing_started_at": "2025-11-17T20:30:10Z",
    "completed_at": "2025-11-17T20:35:00Z",
    "error_message": null,
    "retry_count": 0
  },
  "errors": null
}
```

**Admin Fields:**
When user has admin scope, additional fields are included:
- `output_s3_uri`: Full S3 URI of the converted audio
- `retry_count`: Number of background processing retry attempts

**Output URL:**
- Only available when status is `Completed`
- Pre-signed URL valid for 12 hours
- Allows direct download of converted audio file

**Error Response (404 Not Found):**
```json
{
  "success": false,
  "message": "Voice conversion not found",
  "data": null,
  "errors": null
}
```

---

### Webhook: Conversion Result (Internal)

Internal webhook endpoint called by the external voice conversion service.

**Request:**
```http
POST /api/v1/voice-conversions/webhooks/conversion-result
X-Webhook-Api-Key: {api_key}
Content-Type: application/json

{
  "inference_id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "status": "SUCCESS",
  "error_message": null
}
```

**Status Values:**
- `SUCCESS`: Conversion completed successfully
- `FAILED`: Conversion failed (error_message should be provided)

**Response (200 OK):**
```json
{
  "success": true,
  "message": null,
  "data": {
    "message": "Conversion result processed successfully"
  },
  "errors": null
}
```

**Error Response (400 Bad Request):**
```json
{
  "success": false,
  "message": "Voice conversion not found: {inference_id}",
  "data": null,
  "errors": null
}
```

**Security:**
- Requires `X-Webhook-Api-Key` header matching `Webhooks:ApiKey` configuration
- Returns 401 Unauthorized if API key is missing or invalid

---

## Background Processing

A Lambda function processes pending conversions every 5 minutes:

**Lambda: VoiceByAuribusConversionProcessor**
- **Trigger**: EventBridge rule (rate: 5 minutes)
- **Function**: Checks pending conversions and queues them when preprocessing completes
- **Retry Logic**: Max 5 attempts with 5-minute delay between retries
- **Error Handling**: Automatically fails conversions if preprocessing failed or max retries exceeded

**Processing Flow:**
1. Find conversions with status `PendingPreprocessing`
2. Check audio preprocessing status
3. If preprocessing completed → queue conversion to SQS
4. If preprocessing failed → mark conversion as failed
5. If preprocessing still pending → increment retry count and wait

---

## SQS Message Format

When a conversion is queued, the following message is sent to the voice inference queue:

```json
{
  "inference_id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "order_id": -1,
  "voice_model_path": "s3://bucket/models/artist-model.pth",
  "voice_model_index_path": "s3://bucket/models/artist-model.index",
  "transposition": 0,
  "s3_key_for_inference": "s3://bucket/audio-files/{user_id}/inference/{file_id}.mp3",
  "s3_key_out": "s3://bucket/audio-files/{user_id}/converted/{file_id}_{conversion_id}.mp3"
}
```

**Field Details:**
- `inference_id`: Voice conversion ID (used in webhook callback)
- `order_id`: Always -1 (reserved for future use)
- `voice_model_path`: S3 URI of the AI voice model file
- `voice_model_index_path`: S3 URI of the model index file
- `transposition`: Semitone shift value (integer from Transposition enum)
- `s3_key_for_inference`: Full S3 URI of preprocessed audio file
- `s3_key_out`: Full S3 URI where converted audio should be saved

---

## Database Schema

**Table: voice_conversions**

```sql
CREATE TABLE voice_conversions (
    "Id" uuid PRIMARY KEY,
    "UserId" uuid,
    "AudioFileId" uuid NOT NULL,
    "VoiceModelId" uuid NOT NULL,
    "Transposition" text NOT NULL,
    "Status" text NOT NULL,
    "OutputS3Uri" varchar(500),
    "QueuedAt" timestamptz,
    "ProcessingStartedAt" timestamptz,
    "CompletedAt" timestamptz,
    "ErrorMessage" varchar(1000),
    "RetryCount" integer NOT NULL DEFAULT 0,
    "LastRetryAt" timestamptz,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NOT NULL,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "FK_voice_conversions_audio_files" 
        FOREIGN KEY ("AudioFileId") REFERENCES audio_files ("Id"),
    CONSTRAINT "FK_voice_conversions_voice_models" 
        FOREIGN KEY ("VoiceModelId") REFERENCES voice_models ("Id")
);

CREATE INDEX "IX_voice_conversions_UserId" ON voice_conversions ("UserId");
CREATE INDEX "IX_voice_conversions_AudioFileId" ON voice_conversions ("AudioFileId");
CREATE INDEX "IX_voice_conversions_VoiceModelId" ON voice_conversions ("VoiceModelId");
CREATE INDEX "IX_voice_conversions_Status" ON voice_conversions ("Status");
CREATE INDEX "ix_voice_conversions_status_retry_count" 
    ON voice_conversions ("Status", "RetryCount");
```

---

## Configuration

**appsettings.json:**
```json
{
  "AWS": {
    "S3": {
      "AudioFilesBucket": "voice-by-auribus-api"
    },
    "SQS": {
      "VoiceInferenceQueueUrl": "https://sqs.us-east-1.amazonaws.com/ACCOUNT_ID/voice-inference-queue"
    }
  },
  "Webhooks": {
    "ApiKey": "SECURE_WEBHOOK_API_KEY"
  }
}
```

**Lambda Environment Variables:**
```
ConnectionStrings__DefaultConnection=Host=rds-endpoint;Port=5432;Database=db;Username=user;Password=pass
AWS__Region=us-east-1
AWS__S3__AudioFilesBucket=voice-by-auribus-api
AWS__SQS__VoiceInferenceQueueUrl=https://sqs.us-east-1.amazonaws.com/ACCOUNT_ID/voice-inference-queue
```

---

## Architecture Diagram

```
┌─────────────┐     1. POST /voice-conversions      ┌──────────────┐
│   Client    │────────────────────────────────────▶│  API (App    │
│             │                                      │   Runner)    │
└─────────────┘                                      └──────────────┘
                                                            │
                                                            │ 2. Check preprocessing
                                                            ▼
                                                     ┌──────────────┐
                                                     │  PostgreSQL  │
                                                     │  (RDS)       │
                                                     └──────────────┘
                                                            │
     ┌──────────────────────────────────────────────────────┤
     │                                                       │
     │ 3a. If preprocessing complete                        │ 3b. If preprocessing pending
     ▼                                                       ▼
┌──────────────┐                                     ┌──────────────┐
│  SQS Queue   │                                     │   Status:    │
│ (Inference)  │                                     │  Pending...  │
└──────────────┘                                     └──────────────┘
     │                                                       │
     │ 4. Process audio                                     │
     ▼                                                       │
┌──────────────┐                                            │
│  External    │                                            │
│  Inference   │                                            │
│  Service     │                                            │
└──────────────┘                                            │
     │                                                       │
     │ 5. Webhook callback                                  │
     ▼                                                       │
┌──────────────┐                                            │
│  API Webhook │                                            │
│  /conversion │                                            │
│  -result     │                                            │
└──────────────┘                                            │
     │                                                       │
     │ 6. Update status                                     │
     ▼                                                       ▼
┌──────────────┐    ┌───────────────────────────────────────────┐
│  PostgreSQL  │◀───│  Lambda (EventBridge every 5 min)         │
│              │    │  - Checks pending conversions             │
│              │───▶│  - Queues when preprocessing completes    │
└──────────────┘    └───────────────────────────────────────────┘
```

---

## Testing

Use the following curl examples to test the API:

**Create conversion:**
```bash
curl -X POST http://localhost:5037/api/v1/voice-conversions \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "audio_file_id": "YOUR_AUDIO_FILE_ID",
    "voice_model_id": "YOUR_VOICE_MODEL_ID",
    "transposition": "SameOctave"
  }'
```

**Get conversion:**
```bash
curl http://localhost:5037/api/v1/voice-conversions/{conversion_id} \
  -H "Authorization: Bearer YOUR_TOKEN"
```

**Webhook (internal testing):**
```bash
curl -X POST http://localhost:5037/api/v1/voice-conversions/webhook/conversion-result \
  -H "X-Webhook-Api-Key: YOUR_WEBHOOK_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "inference_id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "status": "SUCCESS"
  }'
```
