# Audio Files API Documentation

## Overview

The Audio Files feature allows users to upload audio files that can be used for voice inference and previews. Each audio file belongs to the user who uploaded it and goes through a preprocessing pipeline to prepare it for inference.

## Architecture Flow

```
1. Client creates audio file record → Backend generates pre-signed S3 upload URL
2. Client uploads file to S3 using pre-signed URL
3. S3 triggers Lambda → Lambda notifies backend of upload
4. Backend updates file status → Triggers preprocessing via SQS
5. External preprocessing service processes audio → Sends result webhook to backend
6. Backend updates preprocessing status and audio metadata
```

## Authentication

All user-facing endpoints require JWT authentication with either:
- `voice-by-auribus-api/base` scope (regular users)
- `voice-by-auribus-api/admin` scope (admin users)

Webhook endpoints require `X-Webhook-Api-Key` header matching the configured API key.

## Endpoints

### POST /api/v1/audio-files

Creates a new audio file record and returns a pre-signed URL for uploading.

**Authorization**: Base scope required

**Request Body:**
```json
{
  "file_name": "my-audio.mp3",
  "file_size": 5242880,
  "mime_type": "audio/mpeg"
}
```

**Validation:**
- `file_name`: Required, max 255 characters
- `file_size`: Required, > 0, ≤ configured max size (default 100MB)
- `mime_type`: Required, must start with "audio/"

**Response (201 Created):**
```json
{
  "success": true,
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "file_name": "my-audio.mp3",
    "file_size": 5242880,
    "mime_type": "audio/mpeg",
    "upload_status": "AwaitingUpload",
    "upload_url": "https://s3.amazonaws.com/...",
    "upload_url_expires_at": "2025-01-15T12:30:00Z",
    "created_at": "2025-01-15T12:00:00Z"
  }
}
```

**Upload URL Usage:**
```bash
curl -X PUT "{{upload_url}}" \
  -H "Content-Type: audio/mpeg" \
  --data-binary "@my-audio.mp3"
```

---

### POST /api/v1/audio-files/{id}/regenerate-upload-url

Regenerates the upload URL for a file that hasn't been uploaded yet.

**Authorization**: Base scope required

**Conditions**: Only works for files with `upload_status` = `AwaitingUpload`

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "upload_url": "https://s3.amazonaws.com/...",
    "upload_url_expires_at": "2025-01-15T13:00:00Z"
  }
}
```

**Error Response (400 Bad Request):**
```json
{
  "success": false,
  "message": "Upload URL can only be regenerated for files awaiting upload"
}
```

---

### GET /api/v1/audio-files/{id}

Retrieves a specific audio file by ID.

**Authorization**: Base scope required

**Response (200 OK) - Regular User:**
```json
{
  "success": true,
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "file_name": "my-audio.mp3",
    "file_size": 5242880,
    "mime_type": "audio/mpeg",
    "upload_status": "Uploaded",
    "is_processed": true,
    "created_at": "2025-01-15T12:00:00Z",
    "updated_at": "2025-01-15T12:05:00Z"
  }
}
```

**Response (200 OK) - Admin User:**
```json
{
  "success": true,
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "file_name": "my-audio.mp3",
    "file_size": 5242880,
    "mime_type": "audio/mpeg",
    "upload_status": "Uploaded",
    "is_processed": true,
    "s3_uri": "s3://voice-by-auribus-audio-files/audio-files/{userId}/temp/{fileId}.mp3",
    "preprocessing": {
      "status": "Completed",
      "audio_duration_seconds": 123.456,
      "s3_uri_short": "s3://voice-by-auribus-audio-files/audio-files/{userId}/short/{fileId}.mp3",
      "s3_uri_inference": "s3://voice-by-auribus-audio-files/audio-files/{userId}/inference/{fileId}.mp3",
      "processing_started_at": "2025-01-15T12:05:30Z",
      "processing_completed_at": "2025-01-15T12:10:00Z",
      "error_message": null
    },
    "created_at": "2025-01-15T12:00:00Z",
    "updated_at": "2025-01-15T12:10:00Z"
  }
}
```

---

### GET /api/v1/audio-files

Gets paginated list of user's audio files.

**Authorization**: Base scope required

**Query Parameters:**
- `page` (optional, default: 1): Page number
- `pageSize` (optional, default: 20, max: 100): Items per page

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "file_name": "my-audio.mp3",
        "file_size": 5242880,
        "mime_type": "audio/mpeg",
        "upload_status": "Uploaded",
        "is_processed": true,
        "created_at": "2025-01-15T12:00:00Z",
        "updated_at": "2025-01-15T12:05:00Z"
      }
    ],
    "pagination": {
      "page": 1,
      "page_size": 20,
      "total_count": 45,
      "total_pages": 3
    }
  }
}
```

---

### DELETE /api/v1/audio-files/{id}

Soft deletes an audio file (sets `is_deleted` flag).

**Authorization**: Base scope required

**Response (204 No Content)**: File successfully deleted

**Response (404 Not Found)**: File not found

---

### POST /api/v1/audio-files/webhooks/upload-notification

**Internal webhook endpoint** - Called by AWS Lambda when file is uploaded to S3.

**Authentication**: Requires `X-Webhook-Api-Key` header

**Request Body:**
```json
{
  "s3_uri": "s3://voice-by-auribus-audio-files/audio-files/{userId}/temp/{fileId}.mp3"
}
```

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "message": "Upload notification processed successfully"
  }
}
```

**Side Effects:**
1. Updates audio file `upload_status` to `Uploaded`
2. Triggers preprocessing by sending message to SQS queue

---

### POST /api/v1/audio-files/webhooks/preprocessing-result

**Internal webhook endpoint** - Called by external preprocessing service when processing completes.

**Authentication**: Requires `X-Webhook-Api-Key` header

**Request Body (Success):**
```json
{
  "s3_key_temp": "s3://voice-by-auribus-audio-files/audio-files/{userId}/temp/{fileId}.mp3",
  "audio_duration": 123,
  "success": true,
  "request_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

**Request Body (Failure):**
```json
{
  "s3_key_temp": "s3://voice-by-auribus-audio-files/audio-files/{userId}/temp/{fileId}.mp3",
  "audio_duration": null,
  "success": false,
  "request_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `s3_key_temp` | string | Full S3 URI of the original input file. Format: `s3://bucket/path` |
| `audio_duration` | int \| null | Duration in seconds, null on failure |
| `success` | boolean | `true` if processing succeeded, `false` otherwise |
| `request_id` | string \| undefined | Original request ID (AudioFileId) if provided |

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "message": "Preprocessing result processed successfully"
  }
}
```

**Side Effects:**
1. Updates preprocessing `processing_status` to `Completed` or `Failed`
2. Sets `audio_duration_seconds` if successful
3. Sets `processing_completed_at` timestamp
4. Validates `request_id` correlation with AudioFileId (logs warning on mismatch)

---

## Data Models

### AudioFile

| Field | Type | Description |
|-------|------|-------------|
| `id` | GUID | Unique identifier |
| `user_id` | GUID | Owner of the file |
| `file_name` | string(255) | Original filename |
| `file_size` | long | Size in bytes |
| `mime_type` | string(100) | Audio MIME type |
| `s3_uri` | string(1000) | S3 URI (admin only) |
| `upload_status` | enum | AwaitingUpload, Uploaded, Failed |
| `is_deleted` | bool | Soft delete flag |
| `created_at` | datetime | Creation timestamp |
| `updated_at` | datetime | Last update timestamp |

### AudioPreprocessing

| Field | Type | Description |
|-------|------|-------------|
| `id` | GUID | Unique identifier |
| `audio_file_id` | GUID | Related audio file |
| `processing_status` | enum | Pending, Processing, Completed, Failed |
| `s3_uri_short` | string(1000) | 10-second preview URI (admin only) |
| `s3_uri_inference` | string(1000) | Processed audio URI (admin only) |
| `audio_duration_seconds` | decimal(18,3) | Duration of processed audio |
| `processing_started_at` | datetime | When processing started |
| `processing_completed_at` | datetime | When processing finished |
| `error_message` | string(2000) | Error details if failed |

---

## S3 Folder Structure

```
s3://voice-by-auribus-audio-files/
└── audio-files/
    └── {userId}/
        ├── temp/
        │   └── {fileId}.{extension}      # Original uploaded file
        ├── short/
        │   └── {fileId}.mp3              # 10-second preview (generated by preprocessing)
        └── inference/
            └── {fileId}.mp3              # Processed file ready for inference
```

---

## Preprocessing Flow

### 1. Trigger Preprocessing

When file upload is confirmed, backend sends message to SQS:

**Queue**: `audio-preprocessing-queue`

**Message:**
```json
{
  "s3_key_temp": "s3://voice-by-auribus-audio-files/audio-files/{userId}/temp/{fileId}.mp3",
  "s3_key_short": "s3://voice-by-auribus-audio-files/audio-files/{userId}/short/{fileId}.mp3",
  "s3_key_for_inference": "s3://voice-by-auribus-audio-files/audio-files/{userId}/inference/{fileId}.mp3",
  "request_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "callback_response": {
    "url": "https://api.example.com/api/v1/audio-files/webhooks/preprocessing-result",
    "type": "HTTP"
  }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `s3_key_temp` | string | Full S3 URI for the temporary (original) uploaded audio file. Format: `s3://bucket/path` |
| `s3_key_short` | string | Full S3 URI where the short preview audio will be stored |
| `s3_key_for_inference` | string | Full S3 URI where the inference-ready audio will be stored |
| `request_id` | string (optional) | Unique identifier for tracking (AudioFileId is used) |
| `callback_response` | object (optional) | Callback configuration for receiving processing results |
| `callback_response.url` | string | Destination URL (HTTP endpoint or SQS queue URL) |
| `callback_response.type` | string | Either `"HTTP"` or `"SQS"` |

### 2. External Service Processing

External service:
1. Reads from SQS queue
2. Downloads file from `s3_key_temp`
3. Processes audio:
   - Generates 10-second preview → uploads to `s3_key_short`
   - Processes for inference → uploads to `s3_key_for_inference`
   - Measures audio duration
4. Calls callback URL (HTTP or SQS) with results

### 3. Callback Notification Response

```json
{
  "s3_key_temp": "s3://voice-by-auribus-audio-files/audio-files/{userId}/temp/{fileId}.mp3",
  "audio_duration": 45,
  "success": true,
  "request_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `s3_key_temp` | string | Full S3 URI of the original input file. Format: `s3://bucket/path` |
| `audio_duration` | int \| null | Duration in seconds, null on failure |
| `success` | boolean | `true` if processing succeeded, `false` otherwise |
| `request_id` | string \| undefined | Original request ID if provided in the request |

### 4. Result Handling

Backend receives result and updates database:
- Success: Sets `status` = `Completed`, stores `audio_duration`
- Failure: Sets `status` = `Failed`, stores `error_message`
- Request ID validation: Logs warning if request_id doesn't match AudioFileId

---

## Security Considerations

1. **User Isolation**: Global query filter ensures users only see their own files
2. **Admin-Only Data**: S3 URIs and preprocessing details hidden from regular users
3. **Pre-signed URLs**: Time-limited (30 min default), PUT-only, size-constrained
4. **Webhook Authentication**: API key validation for internal webhooks
5. **MIME Type Validation**: Only audio files accepted
6. **Soft Delete**: Files are never permanently deleted, only marked as deleted

---

## Configuration

### appsettings.json

```json
{
  "AWS": {
    "S3": {
      "AudioFilesBucket": "voice-by-auribus-audio-files",
      "UploadUrlExpirationMinutes": 30,
      "MaxFileSizeMB": 100
    },
    "SQS": {
      "AudioPreprocessingQueue": "aurivoice-svs-prep-nbl.fifo",
      "PreprocessingCallbackUrl": "https://api.example.com/api/v1/audio-files/webhooks/preprocessing-result",
      "PreprocessingCallbackType": "HTTP"
    }
  },
  "Webhooks": {
    "ApiKey": "SECURE_API_KEY_HERE"
  }
}
```

| Setting | Description |
|---------|-------------|
| `AudioPreprocessingQueue` | Name of the SQS queue for preprocessing messages |
| `PreprocessingCallbackUrl` | URL for receiving preprocessing results (HTTP endpoint or SQS queue URL) |
| `PreprocessingCallbackType` | Callback type: `"HTTP"` or `"SQS"` |

---

## Error Scenarios

### Upload URL Expired
- **Solution**: Call `/regenerate-upload-url` endpoint
- **Prevention**: Upload within 30 minutes of creation

### File Too Large
- **Error**: 400 Bad Request during creation
- **Solution**: Compress audio or split into smaller files
- **Prevention**: Check file size before creating record

### Preprocessing Failed
- **Indicator**: `is_processed` = false after extended time
- **Admin View**: Check `preprocessing.error_message`
- **Solution**: May require re-upload or manual intervention

### Webhook Authentication Failed
- **Error**: 401 Unauthorized on webhook endpoints
- **Solution**: Verify `X-Webhook-Api-Key` header matches configuration
- **Check**: Compare Lambda env var with backend appsettings

---

## Related Resources

- [Implementation Plan](../AUDIO_FILES_FEATURE_IMPLEMENTATION_PLAN.md)
- [AWS Resources Setup](../AWS_RESOURCES.md)
- [Lambda Function Documentation](../../VoiceByAuribus.AudioUploadNotifier/README.md)
