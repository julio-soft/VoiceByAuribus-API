# S3 Event Filtering for Audio File Uploads

## Overview

This document explains how to configure S3 event filtering to ensure the Lambda function only processes events triggered by actual user uploads via pre-signed URLs.

## Filtering Strategy

We use a **two-layer filtering approach**:

1. **S3 Event Configuration**: Filter at the source to reduce Lambda invocations
2. **Lambda Code Validation**: Additional validation for defense in depth

---

## Layer 1: S3 Event Configuration (Recommended)

Configure S3 bucket notifications to filter events before invoking the Lambda function.

### Relevant S3 Events for Pre-Signed URL Uploads

When a user uploads a file using a pre-signed URL, S3 triggers one of these events:

| Event Type | When It Triggers | Use Case |
|------------|------------------|----------|
| `s3:ObjectCreated:Put` | Direct PUT request completes | Small files uploaded in a single request |
| `s3:ObjectCreated:CompleteMultipartUpload` | Multipart upload completes | Large files uploaded in multiple parts |

**Other ObjectCreated events (NOT needed for pre-signed URLs)**:
- `s3:ObjectCreated:Post` - Triggered by browser POST uploads (HTML forms)
- `s3:ObjectCreated:Copy` - Triggered by S3 copy operations
- `s3:ObjectCreated:*` - All creation events (too broad, avoid)

### Configuration via AWS CLI

```bash
aws s3api put-bucket-notification-configuration \
  --bucket voicebyauribus-audio-files \
  --notification-configuration file://notification-config.json
```

**notification-config.json**:
```json
{
  "LambdaFunctionConfigurations": [
    {
      "Id": "AudioFileUploadNotification",
      "LambdaFunctionArn": "arn:aws:lambda:us-east-1:ACCOUNT_ID:function:VoiceByAuribusAudioUploadNotifier",
      "Events": [
        "s3:ObjectCreated:Put",
        "s3:ObjectCreated:CompleteMultipartUpload"
      ],
      "Filter": {
        "Key": {
          "FilterRules": [
            {
              "Name": "prefix",
              "Value": "audio-files/"
            },
            {
              "Name": "suffix",
              "Value": ".mp3"
            }
          ]
        }
      }
    }
  ]
}
```

### Configuration via Terraform

```hcl
resource "aws_s3_bucket_notification" "audio_upload_notification" {
  bucket = aws_s3_bucket.audio_files.id

  lambda_function {
    id                  = "AudioFileUploadNotification"
    lambda_function_arn = aws_lambda_function.audio_upload_notifier.arn
    events              = [
      "s3:ObjectCreated:Put",
      "s3:ObjectCreated:CompleteMultipartUpload"
    ]
    filter_prefix       = "audio-files/"
    filter_suffix       = ".mp3"
  }

  depends_on = [aws_lambda_permission.allow_s3]
}

resource "aws_lambda_permission" "allow_s3" {
  statement_id  = "AllowExecutionFromS3Bucket"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.audio_upload_notifier.function_name
  principal     = "s3.amazonaws.com"
  source_arn    = aws_s3_bucket.audio_files.arn
}
```

### Configuration via AWS Console

1. Go to S3 Console → Select bucket → **Properties** tab
2. Scroll to **Event notifications** → Click **Create event notification**
3. Configure:
   - **Event name**: `AudioFileUploadNotification`
   - **Prefix**: `audio-files/`
   - **Suffix**: `.mp3` (add multiple suffixes for different formats)
   - **Event types**:
     - ✅ `PUT` (under "Object creation")
     - ✅ `Complete multipart upload` (under "Object creation")
   - **Destination**: Lambda function
   - **Lambda function**: `VoiceByAuribusAudioUploadNotifier`

### Multiple Audio Formats

If you support multiple audio formats, you have two options:

**Option A: Multiple notification configurations** (one per format):
```json
{
  "LambdaFunctionConfigurations": [
    {
      "Id": "AudioFileUploadNotification-MP3",
      "LambdaFunctionArn": "arn:aws:lambda:...",
      "Events": ["s3:ObjectCreated:Put", "s3:ObjectCreated:CompleteMultipartUpload"],
      "Filter": {
        "Key": {
          "FilterRules": [
            {"Name": "prefix", "Value": "audio-files/"},
            {"Name": "suffix", "Value": ".mp3"}
          ]
        }
      }
    },
    {
      "Id": "AudioFileUploadNotification-WAV",
      "LambdaFunctionArn": "arn:aws:lambda:...",
      "Events": ["s3:ObjectCreated:Put", "s3:ObjectCreated:CompleteMultipartUpload"],
      "Filter": {
        "Key": {
          "FilterRules": [
            {"Name": "prefix", "Value": "audio-files/"},
            {"Name": "suffix", "Value": ".wav"}
          ]
        }
      }
    }
  ]
}
```

**Option B: Use prefix-only filter + Lambda validation** (recommended):
```json
{
  "LambdaFunctionConfigurations": [
    {
      "Id": "AudioFileUploadNotification",
      "LambdaFunctionArn": "arn:aws:lambda:...",
      "Events": ["s3:ObjectCreated:Put", "s3:ObjectCreated:CompleteMultipartUpload"],
      "Filter": {
        "Key": {
          "FilterRules": [
            {"Name": "prefix", "Value": "audio-files/"}
          ]
        }
      }
    }
  ]
}
```

Then validate extensions in Lambda (already implemented in Layer 2).

---

## Layer 2: Lambda Code Validation (Defense in Depth)

The Lambda function includes additional validation to ensure only valid uploads are processed.

### Implemented Filters

The Lambda function validates three criteria:

#### 1. Event Type Filter

```csharp
private static bool IsUploadCompletionEvent(string eventName)
{
    // Only process events that indicate a completed upload
    // - s3:ObjectCreated:Put - Direct PUT upload (small files, typical with pre-signed URLs)
    // - s3:ObjectCreated:CompleteMultipartUpload - Multipart upload completion (large files)
    return eventName.Contains("ObjectCreated:Put") ||
           eventName.Contains("ObjectCreated:CompleteMultipartUpload");
}
```

**Purpose**: Ensures we only process actual upload completion events, not copies, moves, or other operations.

**Events Filtered Out**:
- `s3:ObjectCreated:Post`
- `s3:ObjectCreated:Copy`
- `s3:ObjectRemoved:*`
- `s3:ReducedRedundancyLostObject`
- etc.

#### 2. Path Validation

```csharp
private static bool IsValidAudioFilePath(string objectKey)
{
    // Expected format: audio-files/{userId}/temp/{fileId}.{extension}
    // Example: audio-files/123e4567-e89b-12d3-a456-426614174000/temp/file.mp3
    return objectKey.StartsWith("audio-files/") && objectKey.Contains("/temp/");
}
```

**Purpose**: Ensures we only process files in the correct location.

**Valid Paths**:
- ✅ `audio-files/123e4567-e89b-12d3-a456-426614174000/temp/file.mp3`
- ✅ `audio-files/user-id-here/temp/audio.wav`

**Invalid Paths** (filtered out):
- ❌ `voice-models/model.pth` (different feature)
- ❌ `audio-files/123e4567-e89b-12d3-a456-426614174000/short/file.mp3` (processed file)
- ❌ `audio-files/123e4567-e89b-12d3-a456-426614174000/inference/file.mp3` (processed file)
- ❌ `other-folder/file.mp3` (wrong bucket structure)

#### 3. Audio File Extension Validation

```csharp
private static bool IsAudioFile(string objectKey)
{
    var audioExtensions = new[] { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a", ".wma", ".opus" };
    return audioExtensions.Any(ext => objectKey.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
}
```

**Purpose**: Ensures we only process audio files.

**Supported Extensions**:
- `.mp3` - MPEG Audio Layer 3
- `.wav` - Waveform Audio File Format
- `.flac` - Free Lossless Audio Codec
- `.aac` - Advanced Audio Coding
- `.ogg` - Ogg Vorbis
- `.m4a` - MPEG-4 Audio
- `.wma` - Windows Media Audio
- `.opus` - Opus Audio Codec

**Non-Audio Files** (filtered out):
- ❌ `.txt`, `.pdf`, `.zip`, `.exe`, etc.

### Example Lambda Execution Flow

```
S3 Event Received: s3:ObjectCreated:Put
Object Key: audio-files/user123/temp/song.mp3

✅ Check 1: IsUploadCompletionEvent("ObjectCreated:Put") → TRUE
✅ Check 2: IsValidAudioFilePath("audio-files/user123/temp/song.mp3") → TRUE
✅ Check 3: IsAudioFile("audio-files/user123/temp/song.mp3") → TRUE

→ Process and notify backend
```

```
S3 Event Received: s3:ObjectCreated:Copy
Object Key: audio-files/user123/temp/song.mp3

❌ Check 1: IsUploadCompletionEvent("ObjectCreated:Copy") → FALSE

→ Skip processing (log and continue)
```

```
S3 Event Received: s3:ObjectCreated:Put
Object Key: audio-files/user123/short/song.mp3

✅ Check 1: IsUploadCompletionEvent("ObjectCreated:Put") → TRUE
❌ Check 2: IsValidAudioFilePath("audio-files/user123/short/song.mp3") → FALSE

→ Skip processing (processed file, not user upload)
```

---

## Testing Event Filtering

### Test 1: Valid Upload via Pre-Signed URL

```bash
# 1. Generate pre-signed URL via API
curl -X POST "http://localhost:5037/api/v1/audio-files" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"file_name":"test.mp3","mime_type":"audio/mpeg"}'

# 2. Upload file using pre-signed URL
curl -X PUT "${UPLOAD_URL}" \
  -H "Content-Type: audio/mpeg" \
  --data-binary "@test.mp3"

# 3. Check CloudWatch Logs for Lambda
# Expected: Lambda processes event and notifies backend
```

### Test 2: Copy Operation (Should Be Filtered)

```bash
# Copy file within S3 (not a user upload)
aws s3 cp \
  s3://bucket/audio-files/user1/temp/file.mp3 \
  s3://bucket/audio-files/user2/temp/file.mp3

# Expected: Lambda skips event (ObjectCreated:Copy)
# CloudWatch Log: "Skipping event ObjectCreated:Copy for ... - not an upload completion event"
```

### Test 3: Invalid Path (Should Be Filtered)

```bash
# Upload to wrong location
aws s3 cp test.mp3 s3://bucket/wrong-path/file.mp3

# Expected: Lambda skips event (invalid path)
# CloudWatch Log: "Skipping object wrong-path/file.mp3 - not in audio-files/*/temp/ path"
```

### Test 4: Non-Audio File (Should Be Filtered)

```bash
# Upload non-audio file
aws s3 cp document.pdf s3://bucket/audio-files/user123/temp/document.pdf

# Expected: Lambda skips event (not audio file)
# CloudWatch Log: "Skipping object ... - not an audio file"
```

---

## CloudWatch Logs Examples

### Successful Processing

```
START RequestId: abc-123-def
Processing 1 S3 event record(s)
Processing upload notification for: s3://bucket/audio-files/user123/temp/file.mp3 (size: 5242880 bytes, event: ObjectCreated:Put)
Sending POST request to: https://api.example.com/api/v1/audio-files/webhook/upload-notification
Backend API responded with status: OK
Successfully notified backend for: s3://bucket/audio-files/user123/temp/file.mp3
END RequestId: abc-123-def
```

### Filtered Event (Not Upload Completion)

```
START RequestId: xyz-789-ghi
Processing 1 S3 event record(s)
Skipping event ObjectCreated:Copy for audio-files/user123/temp/file.mp3 - not an upload completion event
END RequestId: xyz-789-ghi
```

### Filtered Event (Invalid Path)

```
START RequestId: mno-456-pqr
Processing 1 S3 event record(s)
Skipping object audio-files/user123/short/file.mp3 - not in audio-files/*/temp/ path
END RequestId: mno-456-pqr
```

### Filtered Event (Non-Audio File)

```
START RequestId: stu-012-vwx
Processing 1 S3 event record(s)
Skipping object audio-files/user123/temp/document.pdf - not an audio file
END RequestId: stu-012-vwx
```

---

## Benefits of Two-Layer Filtering

### Layer 1 (S3 Configuration)
- ✅ Reduces Lambda invocations (cost savings)
- ✅ Reduces CloudWatch Logs (cost savings)
- ✅ Faster response (no Lambda cold starts for irrelevant events)

### Layer 2 (Lambda Code)
- ✅ Defense in depth (catches edge cases)
- ✅ Detailed logging (easier debugging)
- ✅ Flexible (easy to change without AWS infrastructure updates)
- ✅ Testable (unit tests for filter logic)

---

## Cost Optimization

### Without Filtering

Scenario: 10,000 uploads/day + 5,000 other S3 operations (copies, deletes, etc.)

**Lambda Invocations**: 15,000/day
**Cost**: ~$0.30/day (15,000 × $0.00002)

### With S3 Event Filtering

Scenario: Same as above

**Lambda Invocations**: 10,000/day (only uploads)
**Cost**: ~$0.20/day (10,000 × $0.00002)

**Savings**: ~33% reduction in Lambda costs

### With Both Layers

Even if S3 filtering isn't perfect (e.g., can't filter by extension easily), Layer 2 ensures:
- No unnecessary backend API calls
- No database queries for invalid files
- Clean CloudWatch Logs with clear skip messages

---

## Monitoring and Alerts

### Recommended CloudWatch Metrics

1. **Lambda Invocations**: Monitor total invocations
2. **Filtered Events**: Create metric filter for "Skipping" logs
3. **Backend API Errors**: Create metric filter for "Backend API returned error"
4. **Processing Latency**: Track time from upload to notification

### Example Metric Filter (Filtered Events)

```
[time, request_id, type, message = "Skipping*"]
```

**Metric**: `AudioUploadNotifier/FilteredEvents`

### Example Alarm (High Error Rate)

```
Metric: Errors (Lambda built-in)
Threshold: > 10 in 5 minutes
Action: Send SNS notification to ops team
```

---

## Troubleshooting

### Lambda Not Triggering on Upload

**Possible Causes**:
1. S3 event configuration missing or incorrect
2. Lambda permission not granted to S3
3. Event type filter too restrictive

**Debug Steps**:
```bash
# Check S3 notification configuration
aws s3api get-bucket-notification-configuration --bucket voicebyauribus-audio-files

# Check Lambda resource policy
aws lambda get-policy --function-name VoiceByAuribusAudioUploadNotifier

# Test with AWS CLI upload
aws s3 cp test.mp3 s3://voicebyauribus-audio-files/audio-files/test-user/temp/test.mp3
```

### Lambda Triggering on Wrong Events

**Possible Causes**:
1. S3 event configuration too broad (e.g., `s3:ObjectCreated:*`)
2. Missing path prefix/suffix filters

**Debug Steps**:
- Check CloudWatch Logs for event names
- Review S3 notification configuration
- Update event type filters to be more specific

### Backend API Not Receiving Notifications

**Possible Causes**:
1. Lambda filtering out valid uploads
2. Backend API URL incorrect
3. Webhook API key mismatch

**Debug Steps**:
- Check CloudWatch Logs for "Skipping" messages
- Verify Lambda environment variables: `API_BASE_URL`, `WEBHOOK_API_KEY`
- Test backend webhook endpoint manually with curl

---

## Summary

**Recommended Configuration**:

1. **S3 Event Notification**:
   - Events: `s3:ObjectCreated:Put`, `s3:ObjectCreated:CompleteMultipartUpload`
   - Prefix: `audio-files/`
   - Suffix: None (validate in Lambda for flexibility)

2. **Lambda Validation** (already implemented):
   - Event type check
   - Path validation (`/temp/` folder only)
   - Audio file extension check

This two-layer approach provides:
- ✅ Cost optimization (reduced Lambda invocations)
- ✅ Defense in depth (catch edge cases)
- ✅ Flexibility (easy to modify filters)
- ✅ Clear logging (easy to debug)

**Result**: Lambda only processes actual user uploads via pre-signed URLs, ignoring all other S3 operations.
