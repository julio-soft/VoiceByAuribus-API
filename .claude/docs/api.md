# API Documentation

## Overview

VoiceByAuribus API provides RESTful endpoints for managing voice models and audio files. All endpoints use JSON for request/response bodies with `snake_case_lower` naming convention.

## Base URL

```
http://localhost:5037/api/v1
```

## Authentication

The API uses **AWS Cognito M2M (Machine-to-Machine) authentication** with JWT bearer tokens.

### Authorization Header

```
Authorization: Bearer <jwt_token>
```

### Scopes

- `voice-by-auribus-api/base`: Required for all user-facing endpoints
- `voice-by-auribus-api/admin`: Required for admin-specific features

**Note**: Cognito M2M tokens don't include standard `aud` claim. See `.ai_doc/COGNITO_M2M_AUTH.md` for details.

### Webhook Authentication

Internal webhook endpoints use API key authentication:

```
X-Webhook-Api-Key: <your_api_key>
```

## Response Format

All endpoints return a standardized `ApiResponse<T>` wrapper:

### Success Response

```json
{
  "success": true,
  "message": "Optional success message",
  "data": {
    /* Response data */
  }
}
```

### Error Response

```json
{
  "success": false,
  "message": "Error description",
  "errors": ["Validation error 1", "Validation error 2"]
}
```

### Validation Error (400)

```json
{
  "success": false,
  "message": "Validation failed",
  "errors": [
    "file_name: File name is required",
    "mime_type: Only audio files are allowed"
  ]
}
```

## API Versioning

URL-based versioning: `/api/v{version}/{resource}`

Current version: `v1`

---

# Auth Endpoints

## GET /api/v1/auth/current-user

Retrieves information about the currently authenticated user.

### Authorization

- **Scope**: `voice-by-auribus-api/base`

### Response (200 OK)

```json
{
  "success": true,
  "data": {
    "user_id": "123e4567-e89b-12d3-a456-426614174000",
    "sub": "cognito-user-sub",
    "scopes": ["voice-by-auribus-api/base"],
    "is_admin": false
  }
}
```

### Error Responses

- **401 Unauthorized**: Missing or invalid token
- **403 Forbidden**: Insufficient scopes

---

## GET /api/v1/auth/status

Returns authentication status and token information.

### Authorization

- **Scope**: `voice-by-auribus-api/base`

### Response (200 OK)

```json
{
  "success": true,
  "data": {
    "authenticated": true,
    "user_id": "123e4567-e89b-12d3-a456-426614174000",
    "scopes": ["voice-by-auribus-api/base"],
    "is_admin": false,
    "token_expires_at": "2025-01-15T14:00:00Z"
  }
}
```

### Error Responses

- **401 Unauthorized**: Missing or invalid token
- **403 Forbidden**: Insufficient scopes

---

# Voice Model Endpoints

## GET /api/v1/voices

Retrieves list of all available voice models.

### Authorization

- **Scope**: `voice-by-auribus-api/base`

### Response (200 OK) - Regular User

```json
{
  "success": true,
  "data": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "name": "Voice Model 1",
      "description": "Professional male voice",
      "language": "en-US",
      "created_at": "2025-01-10T10:00:00Z",
      "updated_at": "2025-01-10T10:00:00Z"
    },
    {
      "id": "660e8400-e29b-41d4-a716-446655440001",
      "name": "Voice Model 2",
      "description": "Professional female voice",
      "language": "es-ES",
      "created_at": "2025-01-11T12:00:00Z",
      "updated_at": "2025-01-11T12:00:00Z"
    }
  ]
}
```

### Response (200 OK) - Admin User

Admin users receive additional fields with S3 pre-signed download URLs:

```json
{
  "success": true,
  "data": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "name": "Voice Model 1",
      "description": "Professional male voice",
      "language": "en-US",
      "voice_model_url": "https://s3.amazonaws.com/...",
      "voice_model_index_url": "https://s3.amazonaws.com/...",
      "created_at": "2025-01-10T10:00:00Z",
      "updated_at": "2025-01-10T10:00:00Z"
    }
  ]
}
```

**Note**: Pre-signed URLs expire after 12 hours

### Error Responses

- **401 Unauthorized**: Missing or invalid token
- **403 Forbidden**: Insufficient scopes

---

## GET /api/v1/voices/{id}

Retrieves a specific voice model by ID.

### Authorization

- **Scope**: `voice-by-auribus-api/base`

### Path Parameters

- `id` (GUID): Voice model unique identifier

### Response (200 OK) - Regular User

```json
{
  "success": true,
  "data": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "name": "Voice Model 1",
    "description": "Professional male voice",
    "language": "en-US",
    "created_at": "2025-01-10T10:00:00Z",
    "updated_at": "2025-01-10T10:00:00Z"
  }
}
```

### Response (200 OK) - Admin User

```json
{
  "success": true,
  "data": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "name": "Voice Model 1",
    "description": "Professional male voice",
    "language": "en-US",
    "voice_model_url": "https://s3.amazonaws.com/bucket/path?...",
    "voice_model_index_url": "https://s3.amazonaws.com/bucket/path?...",
    "created_at": "2025-01-10T10:00:00Z",
    "updated_at": "2025-01-10T10:00:00Z"
  }
}
```

### Error Responses

- **401 Unauthorized**: Missing or invalid token
- **403 Forbidden**: Insufficient scopes
- **404 Not Found**: Voice model doesn't exist

```json
{
  "success": false,
  "message": "Voice model not found"
}
```

---

# Audio File Endpoints

## POST /api/v1/audio-files

Creates a new audio file record and returns a pre-signed S3 upload URL.

### Authorization

- **Scope**: `voice-by-auribus-api/base`

### Request Body

```json
{
  "file_name": "my-audio.mp3",
  "mime_type": "audio/mpeg"
}
```

### Request Validation

- `file_name`:
  - Required
  - Maximum length: 255 characters
- `mime_type`:
  - Required
  - Must match pattern: `^audio\/.*` (only audio files)

### Response (200 OK)

```json
{
  "success": true,
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "file_name": "my-audio.mp3",
    "mime_type": "audio/mpeg",
    "upload_status": "AwaitingUpload",
    "upload_url": "https://s3.amazonaws.com/bucket/path?...",
    "upload_url_expires_at": "2025-01-15T12:30:00Z",
    "created_at": "2025-01-15T12:00:00Z"
  }
}
```

### Upload URL Usage

Use the `upload_url` to upload your file directly to S3:

```bash
curl -X PUT "${upload_url}" \
  -H "Content-Type: audio/mpeg" \
  --data-binary "@my-audio.mp3"
```

**Important**:
- Upload URL expires in 15 minutes (configurable)
- Maximum file size: 100 MB (configurable)
- Content-Type header must match the MIME type

### Error Responses

- **400 Bad Request**: Validation errors

```json
{
  "success": false,
  "message": "Validation failed",
  "errors": [
    "file_name: File name is required",
    "mime_type: Only audio files are allowed"
  ]
}
```

- **401 Unauthorized**: Missing or invalid token
- **403 Forbidden**: Insufficient scopes

---

## POST /api/v1/audio-files/{id}/regenerate-upload-url

Regenerates the pre-signed upload URL for a file that hasn't been uploaded yet.

### Authorization

- **Scope**: `voice-by-auribus-api/base`

### Path Parameters

- `id` (GUID): Audio file unique identifier

### Conditions

- Only works for files with `upload_status` = `AwaitingUpload`
- File must belong to the authenticated user

### Response (200 OK)

```json
{
  "success": true,
  "data": {
    "upload_url": "https://s3.amazonaws.com/bucket/path?...",
    "upload_url_expires_at": "2025-01-15T13:00:00Z"
  }
}
```

### Error Responses

- **400 Bad Request**: Invalid operation

```json
{
  "success": false,
  "message": "Upload URL can only be regenerated for files awaiting upload"
}
```

- **401 Unauthorized**: Missing or invalid token
- **403 Forbidden**: Insufficient scopes
- **404 Not Found**: Audio file doesn't exist or doesn't belong to user

---

## GET /api/v1/audio-files/{id}

Retrieves a specific audio file by ID.

### Authorization

- **Scope**: `voice-by-auribus-api/base`

### Path Parameters

- `id` (GUID): Audio file unique identifier

### Response (200 OK) - Regular User

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

### Response (200 OK) - Admin User

Admin users receive additional preprocessing details and S3 URIs:

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
    "s3_uri": "s3://bucket/audio-files/{userId}/temp/{fileId}.mp3",
    "preprocessing": {
      "status": "Completed",
      "audio_duration_seconds": 123,
      "s3_uri_short": "s3://bucket/audio-files/{userId}/short/{fileId}.mp3",
      "s3_uri_inference": "s3://bucket/audio-files/{userId}/inference/{fileId}.mp3",
      "processing_started_at": "2025-01-15T12:05:30Z",
      "processing_completed_at": "2025-01-15T12:10:00Z",
      "error_message": null
    },
    "created_at": "2025-01-15T12:00:00Z",
    "updated_at": "2025-01-15T12:10:00Z"
  }
}
```

### Error Responses

- **401 Unauthorized**: Missing or invalid token
- **403 Forbidden**: Insufficient scopes
- **404 Not Found**: Audio file doesn't exist or doesn't belong to user

```json
{
  "success": false,
  "message": "Audio file not found"
}
```

---

## GET /api/v1/audio-files

Retrieves a paginated list of the user's audio files.

### Authorization

- **Scope**: `voice-by-auribus-api/base`

### Query Parameters

- `page` (optional, default: 1): Page number (1-indexed)
- `page_size` (optional, default: 20, max: 100): Items per page

### Example Request

```
GET /api/v1/audio-files?page=2&page_size=10
```

### Response (200 OK)

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
      },
      {
        "id": "4fb85f64-5717-4562-b3fc-2c963f66afa7",
        "file_name": "another-audio.wav",
        "file_size": 8388608,
        "mime_type": "audio/wav",
        "upload_status": "Uploaded",
        "is_processed": false,
        "created_at": "2025-01-15T13:00:00Z",
        "updated_at": "2025-01-15T13:02:00Z"
      }
    ],
    "pagination": {
      "page": 2,
      "page_size": 10,
      "total_count": 45,
      "total_pages": 5
    }
  }
}
```

**Note**: Results are ordered by `created_at` descending (newest first)

### Error Responses

- **401 Unauthorized**: Missing or invalid token
- **403 Forbidden**: Insufficient scopes

---

## DELETE /api/v1/audio-files/{id}

Soft deletes an audio file (sets `is_deleted` flag to true).

### Authorization

- **Scope**: `voice-by-auribus-api/base`

### Path Parameters

- `id` (GUID): Audio file unique identifier

### Response (204 No Content)

File successfully deleted (no response body)

### Error Responses

- **401 Unauthorized**: Missing or invalid token
- **403 Forbidden**: Insufficient scopes
- **404 Not Found**: Audio file doesn't exist or doesn't belong to user

**Note**: Soft deleted files are automatically excluded from all queries due to global query filters.

---

# Webhook Endpoints (Internal)

These endpoints are called by AWS services and require webhook API key authentication.

## POST /api/v1/audio-files/webhook/upload-notification

Called by AWS Lambda when a file is successfully uploaded to S3.

### Authentication

```
X-Webhook-Api-Key: <your_webhook_api_key>
```

### Request Body

```json
{
  "s3_uri": "s3://bucket/audio-files/{userId}/temp/{fileId}.mp3",
  "file_size": 5242880
}
```

### Request Validation

- `s3_uri`:
  - Required
  - Must be a valid S3 URI
- `file_size`:
  - Required
  - Must be greater than 0

### Response (200 OK)

```json
{
  "success": true,
  "data": {
    "message": "Upload notification processed successfully"
  }
}
```

### Side Effects

1. Updates audio file `upload_status` to `Uploaded`
2. Sets `file_size` from S3 event
3. Triggers preprocessing by sending message to SQS queue

### Error Responses

- **401 Unauthorized**: Missing or invalid API key
- **400 Bad Request**: Invalid S3 URI or audio file not found

---

## POST /api/v1/audio-files/webhook/preprocessing-result

Called by external preprocessing service when audio processing completes.

### Authentication

```
X-Webhook-Api-Key: <your_webhook_api_key>
```

### Request Body (Success)

```json
{
  "s3_key_temp": "audio-files/{userId}/temp/{fileId}.mp3",
  "audio_duration": 123
}
```

### Request Body (Failure)

```json
{
  "s3_key_temp": "audio-files/{userId}/temp/{fileId}.mp3",
  "audio_duration": null
}
```

### Response (200 OK)

```json
{
  "success": true,
  "data": {
    "message": "Preprocessing result processed successfully"
  }
}
```

### Side Effects

**On Success:**
1. Updates preprocessing `processing_status` to `Completed`
2. Sets `audio_duration_seconds` from result
3. Sets `processing_completed_at` timestamp
4. Constructs S3 URIs for short and inference files

**On Failure:**
1. Updates preprocessing `processing_status` to `Failed`
2. Sets `processing_completed_at` timestamp
3. Sets `error_message` with failure details

### Error Responses

- **401 Unauthorized**: Missing or invalid API key
- **400 Bad Request**: Invalid S3 key or audio file not found

---

# Data Models

## Audio File Upload States

| State | Description |
|-------|-------------|
| `AwaitingUpload` | File record created, waiting for S3 upload |
| `Uploaded` | File successfully uploaded to S3 |
| `Failed` | Upload failed (not currently used) |

## Preprocessing States

| State | Description |
|-------|-------------|
| `Pending` | Waiting to start preprocessing |
| `Processing` | Currently being processed |
| `Completed` | Successfully processed |
| `Failed` | Processing failed |

## S3 Folder Structure

```
s3://{bucket}/
└── audio-files/
    └── {userId}/
        ├── temp/
        │   └── {fileId}.{ext}      # Original uploaded file
        ├── short/
        │   └── {fileId}.mp3        # 10-second preview
        └── inference/
            └── {fileId}.mp3        # Processed for inference
```

---

# Error Codes

| HTTP Status | Description |
|-------------|-------------|
| 200 OK | Request successful |
| 204 No Content | Request successful (no response body) |
| 400 Bad Request | Validation error or invalid operation |
| 401 Unauthorized | Missing or invalid authentication |
| 403 Forbidden | Insufficient permissions |
| 404 Not Found | Resource doesn't exist |
| 500 Internal Server Error | Server error (logged automatically) |

---

# Rate Limiting

Currently not implemented. All endpoints are available without rate limiting.

**Future consideration**: Implement rate limiting for:
- File upload creation
- Pre-signed URL regeneration
- Webhook endpoints

---

# CORS Configuration

CORS is configured in `Program.cs`. Update allowed origins in production:

```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("https://your-frontend-domain.com")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
```

---

# Testing Endpoints

Use the provided HTTP test file:
- Location: `VoiceByAuribus.API/api-tests.http`
- Requires: [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) VS Code extension

Example test:

```http
### Get Current User
GET {{baseUrl}}/auth/current-user
Authorization: Bearer {{token}}
```

---

# Additional Resources

- [Audio Files Feature Documentation](../../.ai_doc/v1/audio_files.md)
- [Authentication Details](../../.ai_doc/COGNITO_M2M_AUTH.md)
- [AWS Resources](../../.ai_doc/AWS_RESOURCES.md)
- [Architecture Documentation](architecture.md)
