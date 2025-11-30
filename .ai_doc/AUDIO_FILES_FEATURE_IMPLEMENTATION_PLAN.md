# Audio Files Feature - Implementation Plan

## Overview
Feature para gestionar ficheros de audio que serán usados para generar inferencias y previews. Los ficheros pertenecen al usuario que los sube.

## Business Requirements

### 1. Audio File Management
- Cada fichero subido pertenece al usuario que lo subió (user ownership via `IHasUserId`)
- Ficheros se almacenan en S3 (guardar URI en BD: `s3://bucket/key`)
- Soft delete support (implementar `ISoftDelete`)
- Tracking de estado del fichero (uploaded, processing, processed, failed)

### 2. Pre-Signed URL for Upload
- Al crear un fichero nuevo en BD, devolver pre-signed URL PUT para que cliente suba
- Tiempo suficiente para upload: **30 minutos** (configurable)
- Límite de tamaño: **100MB** (configurable)
- Endpoint adicional para regenerar pre-signed URL (solo para ficheros no subidos aún)

### 3. S3 Event Notification Flow
```
Cliente sube archivo → S3 → S3 Event → Lambda → POST /api/v1/audio-files/webhook/upload-notification → Backend actualiza estado
```

### 4. Audio Preprocessing
Cuando el estado cambia a "uploaded", lanzar preprocessing automáticamente:

**Input (mensaje SQS):**
```json
{
  "s3_key_temp": "s3://bucket/audio-files/{userId}/temp/{fileId}.mp3",
  "s3_key_short": "s3://bucket/audio-files/{userId}/short/{fileId}.mp3",
  "s3_key_for_inference": "s3://bucket/audio-files/{userId}/inference/{fileId}.mp3",
  "request_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "callback_response": {
    "url": "https://api.example.com/api/v1/audio-files/webhooks/preprocessing-result",
    "type": "HTTP"
  }
}
```

**Output (webhook callback):**
```json
{
  "s3_key_temp": "s3://bucket/audio-files/{userId}/temp/{fileId}.mp3",
  "audio_duration": 123,
  "success": true,
  "request_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `s3_key_temp` | string | Original input S3 key |
| `audio_duration` | int \| null | Duration in seconds, null on failure |
| `success` | boolean | `true` if processing succeeded, `false` otherwise |
| `request_id` | string \| undefined | Original request ID if provided |

### 5. Preprocessing Data Model
Crear modelo separado para tracking de preprocessing:
- Link al AudioFile
- Estado: pending, processing, completed, failed
- Timestamps de cada etapa
- Audio duration (resultado del preprocessing)
- S3 keys para short y inference files
- **Solo admin puede ver detalles completos**
- **Usuario regular solo ve si está "processed" o no**

### 6. S3 Folder Structure
```
audio-files/
  {userId}/
    temp/
      {fileId}.{extension}
    short/
      {fileId}.mp3
    inference/
      {fileId}.mp3
```

## Technical Architecture

### Domain Models

#### AudioFile Entity
```csharp
- Id (Guid)
- UserId (string) - implements IHasUserId
- FileName (string)
- FileSize (long) - bytes
- MimeType (string) - validation: only audio/*
- S3Uri (string) - s3://bucket/key
- UploadStatus (enum): AwaitingUpload, Uploaded, Failed
- IsDeleted (bool) - implements ISoftDelete
- BaseAuditableEntity fields (CreatedAt, UpdatedAt)
```

#### AudioPreprocessing Entity
```csharp
- Id (Guid)
- AudioFileId (Guid FK)
- AudioFile (navigation)
- ProcessingStatus (enum): Pending, Processing, Completed, Failed
- S3UriShort (string, nullable)
- S3UriInference (string, nullable)
- AudioDurationSeconds (decimal?, nullable)
- ProcessingStartedAt (DateTime?)
- ProcessingCompletedAt (DateTime?)
- ErrorMessage (string?, nullable)
- BaseAuditableEntity fields
```

### Endpoints

#### POST /api/v1/audio-files
**Auth:** Base scope
**Request:**
```json
{
  "file_name": "my-audio.mp3",
  "file_size": 5242880,
  "mime_type": "audio/mpeg"
}
```
**Response:**
```json
{
  "success": true,
  "data": {
    "id": "guid",
    "file_name": "my-audio.mp3",
    "file_size": 5242880,
    "mime_type": "audio/mpeg",
    "upload_status": "awaiting_upload",
    "upload_url": "https://presigned-url...",
    "upload_url_expires_at": "2025-01-15T12:30:00Z",
    "created_at": "2025-01-15T12:00:00Z"
  }
}
```

#### POST /api/v1/audio-files/{id}/regenerate-upload-url
**Auth:** Base scope
**Conditions:** Solo si UploadStatus == AwaitingUpload
**Response:**
```json
{
  "success": true,
  "data": {
    "upload_url": "https://presigned-url...",
    "upload_url_expires_at": "2025-01-15T13:00:00Z"
  }
}
```

#### GET /api/v1/audio-files
**Auth:** Base scope
**Query params:** page, pageSize
**Response:** Paginated list of user's audio files

#### GET /api/v1/audio-files/{id}
**Auth:** Base scope
**Response:**
```json
{
  "success": true,
  "data": {
    "id": "guid",
    "file_name": "my-audio.mp3",
    "file_size": 5242880,
    "mime_type": "audio/mpeg",
    "upload_status": "uploaded",
    "is_processed": true,  // derived from preprocessing status
    "created_at": "2025-01-15T12:00:00Z",
    "updated_at": "2025-01-15T12:05:00Z",
    // Admin only:
    "s3_uri": "s3://...",  // if admin
    "preprocessing": {  // if admin
      "status": "completed",
      "audio_duration_seconds": 123.45,
      "s3_uri_short": "s3://...",
      "s3_uri_inference": "s3://...",
      "processing_completed_at": "2025-01-15T12:10:00Z"
    }
  }
}
```

#### DELETE /api/v1/audio-files/{id}
**Auth:** Base scope
**Action:** Soft delete

#### POST /api/v1/audio-files/webhook/upload-notification
**Auth:** Internal only (validar signature o API key específica)
**Request:**
```json
{
  "s3_uri": "s3://bucket/audio-files/{userId}/temp/{fileId}.mp3"
}
```
**Logic:**
1. Buscar AudioFile por s3_uri
2. Actualizar UploadStatus a Uploaded
3. Trigger preprocessing (enviar mensaje SQS)

#### POST /api/v1/audio-files/webhook/preprocessing-result
**Auth:** Internal only
**Request:**
```json
{
  "s3_key_temp": "s3://...",
  "audio_duration": 123.45  // null = failed
}
```
**Logic:**
1. Buscar AudioFile por s3_key_temp
2. Actualizar AudioPreprocessing con resultado
3. Si audio_duration es null → status = Failed
4. Si audio_duration existe → status = Completed

### AWS Lambda Project

#### Proyecto: AudioFileUploadNotifier.Lambda
**Runtime:** .NET 10
**Trigger:** S3 Event (ObjectCreated)
**Dependencies:**
- AWSSDK.S3
- Amazon.Lambda.Core
- Amazon.Lambda.S3Events
- HttpClient

**Logic:**
```csharp
1. Recibir S3Event
2. Extraer bucket + key del evento
3. Construir s3_uri
4. POST a backend endpoint /api/v1/audio-files/webhook/upload-notification
5. Incluir API Key en header para autenticación
```

### AWS Resources Configuration

#### appsettings.json additions:
```json
{
  "AWS": {
    "Region": "us-east-1",
    "Profile": "default",
    "S3": {
      "AudioFilesBucket": "voice-by-auribus-audio-files",
      "UploadUrlExpirationMinutes": 30,
      "MaxFileSizeMB": 100
    },
    "SQS": {
      "AudioPreprocessingQueueUrl": "https://sqs.us-east-1.amazonaws.com/{accountId}/audio-preprocessing-queue"
    }
  },
  "Webhooks": {
    "ApiKey": "your-internal-api-key-here"
  }
}
```

### Services to Implement

#### IAudioFileService
- CreateAudioFileAsync(CreateAudioFileDto)
- RegenerateUploadUrlAsync(Guid id, string userId)
- GetAudioFileByIdAsync(Guid id, string userId, bool isAdmin)
- GetUserAudioFilesAsync(string userId, int page, int pageSize)
- SoftDeleteAsync(Guid id, string userId)
- HandleUploadNotificationAsync(string s3Uri)

#### IAudioPreprocessingService
- TriggerPreprocessingAsync(Guid audioFileId)
- HandlePreprocessingResultAsync(PreprocessingResultDto)
- GetPreprocessingStatusAsync(Guid audioFileId)

#### IS3PresignedUrlService (extend existing)
- Add: CreateUploadUrl(string bucketName, string key, TimeSpan lifetime, long maxFileSize)

#### ISqsService (new)
- SendMessageAsync<T>(string queueUrl, T message)

### Validation

#### CreateAudioFileValidator
- FileName: required, max 255 chars
- FileSize: required, > 0, <= MaxFileSizeMB config
- MimeType: required, must start with "audio/"

### Infrastructure

#### DI Registration
- AddScoped<IAudioFileService, AudioFileService>
- AddScoped<IAudioPreprocessingService, AudioPreprocessingService>
- AddSingleton<ISqsService, SqsService>
- AddSingleton IAmazonSQS from AWS SDK

#### EF Core Configurations
- AudioFileConfiguration.cs
- AudioPreprocessingConfiguration.cs

### Security Considerations

1. **User Isolation:** Global filter via IHasUserId ensures users only see their files
2. **Admin Privileges:** Sensitive S3 URIs and preprocessing details only for admin
3. **Webhook Authentication:** API Key validation for internal webhooks
4. **Pre-signed URL Security:**
   - Short expiration (30 min)
   - PUT-only for upload
   - File size limit enforced
5. **Input Validation:** MIME type must be audio/*, size limits

## Implementation Steps

### Phase 1: Domain & Infrastructure
1. Create AudioFile and AudioPreprocessing entities
2. Create EF Core configurations in Shared/Infrastructure/Data/Configurations/
3. Create and apply migration
4. Extend IS3PresignedUrlService interface
5. Update S3PresignedUrlService implementation
6. Create ISqsService interface and implementation
7. Update appsettings.json with AWS config

### Phase 2: Application Layer
8. Create DTOs for all operations
9. Create FluentValidation validators
10. Create IAudioFileService and implementation
11. Create IAudioPreprocessingService and implementation
12. Create mapping helpers

### Phase 3: Presentation Layer
13. Create AudioFilesController with all endpoints
14. Create webhook authentication filter/middleware
15. Add proper authorization attributes

### Phase 4: Feature Module
16. Create AudioFilesModule.cs with DI registration
17. Register in Program.cs

### Phase 5: AWS Lambda
18. Create new .NET 10 Lambda project in solution
19. Implement S3 event handler
20. Configure Lambda deployment files
21. Add Lambda documentation

### Phase 6: Documentation
22. Create .ai_doc/v1/audio_files.md
23. Update .github/copilot-instructions.md with new feature reference
24. Create .ai_doc/AWS_RESOURCES.md with S3/SQS/Lambda setup instructions

## Testing Strategy

### api-tests.http additions:
- POST create audio file
- POST regenerate upload url
- GET list audio files
- GET audio file by id
- DELETE audio file
- POST upload notification webhook
- POST preprocessing result webhook

## Notes

- Preprocessing es asíncrono: no bloquea el response de upload notification
- Usuario solo ve "is_processed" boolean en respuesta regular
- Admin ve todos los detalles de preprocessing
- Lambda debe manejar reintentos si backend no responde
- SQS message debe incluir metadata para debugging (userId, fileId, timestamps)
- Considerar DLQ para mensajes SQS fallidos
