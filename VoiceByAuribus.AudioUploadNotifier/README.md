# VoiceByAuribus Audio Upload Notifier Lambda

AWS Lambda function that handles S3 upload events for audio files and notifies the VoiceByAuribus API backend.

## Overview

This Lambda function is triggered when audio files are uploaded to the configured S3 bucket. It extracts the S3 URI from the event and sends a POST request to the backend API's webhook endpoint to notify it of the upload.

## Architecture Flow

```
User uploads audio → S3 Bucket → S3 Event Notification → Lambda Function → Backend API Webhook
```

## Environment Variables

The Lambda function requires the following environment variables to be configured:

- `API_BASE_URL`: The base URL of the VoiceByAuribus API (e.g., `https://api.voicebyauribus.com`)
- `WEBHOOK_API_KEY`: The API key for authenticating webhook requests to the backend

## Deployment

### Prerequisites

- .NET 10.0 SDK
- AWS CLI configured with appropriate credentials
- Amazon.Lambda.Tools global tool installed:
  ```bash
  dotnet tool install -g Amazon.Lambda.Tools
  ```

### Deploy to AWS Lambda

From the `src/VoiceByAuribus.AudioUploadNotifier` directory:

```bash
dotnet lambda deploy-function VoiceByAuribus-AudioUploadNotifier
```

Or use the AWS Lambda tools defaults configuration in `aws-lambda-tools-defaults.json`.

### Configure S3 Event Trigger

After deploying the Lambda function, you need to configure the S3 bucket to trigger this Lambda:

1. Go to AWS S3 Console
2. Select your audio files bucket (e.g., `voice-by-auribus-audio-files`)
3. Go to Properties → Event notifications
4. Create event notification:
   - **Event name**: `AudioFileUploadNotification`
   - **Prefix**: `audio-files/` (to only trigger on audio files)
   - **Event types**: Select "PUT" (ObjectCreated)
   - **Destination**: Lambda function
   - **Lambda function**: Select `VoiceByAuribus-AudioUploadNotifier`

### Set Environment Variables

In the Lambda console, configure the environment variables:

- `API_BASE_URL`: `https://your-api-domain.com`
- `WEBHOOK_API_KEY`: Your secure webhook API key (must match `Webhooks:ApiKey` in backend appsettings.json)

### IAM Permissions

The Lambda execution role needs:

- `s3:GetObject` permission on the audio files bucket
- Standard Lambda execution permissions

Example IAM policy:
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "s3:GetObject",
        "s3:GetObjectMetadata"
      ],
      "Resource": "arn:aws:s3:::voice-by-auribus-audio-files/*"
    },
    {
      "Effect": "Allow",
      "Action": [
        "logs:CreateLogGroup",
        "logs:CreateLogStream",
        "logs:PutLogEvents"
      ],
      "Resource": "arn:aws:logs:*:*:*"
    }
  ]
}
```

## Testing

### Local Testing

Run tests from the test directory:

```bash
cd test/VoiceByAuribus.AudioUploadNotifier.Tests
dotnet test
```

### Manual Testing

You can test the Lambda function by uploading a file to the configured S3 bucket in the correct path:

```bash
aws s3 cp test-audio.mp3 s3://voice-by-auribus-audio-files/audio-files/{userId}/temp/{fileId}.mp3
```

Check CloudWatch Logs to verify the Lambda executed successfully and notified the backend.

## Monitoring

View logs in AWS CloudWatch Logs:
- Log Group: `/aws/lambda/VoiceByAuribus-AudioUploadNotifier`

The Lambda logs:
- Number of S3 events received
- S3 URI being processed
- HTTP request details to backend
- Success/failure status

## Error Handling

If the Lambda fails to notify the backend:
- The Lambda will throw an exception and retry automatically (Lambda retry mechanism)
- Failed events can be configured to go to a Dead Letter Queue (DLQ) for investigation
- Check CloudWatch Logs for detailed error messages

## Development

### Project Structure

```
VoiceByAuribus.AudioUploadNotifier/
├── src/
│   └── VoiceByAuribus.AudioUploadNotifier/
│       ├── Function.cs                    # Main Lambda handler
│       ├── aws-lambda-tools-defaults.json # Deployment configuration
│       └── VoiceByAuribus.AudioUploadNotifier.csproj
└── test/
    └── VoiceByAuribus.AudioUploadNotifier.Tests/
```

### Dependencies

- `Amazon.Lambda.Core` - Lambda runtime
- `Amazon.Lambda.S3Events` - S3 event types
- `Amazon.Lambda.Serialization.SystemTextJson` - JSON serialization

## Related Documentation

- [Audio Files Feature Implementation Plan](../../.ai_doc/AUDIO_FILES_FEATURE_IMPLEMENTATION_PLAN.md)
- [AWS Resources Documentation](../../.ai_doc/AWS_RESOURCES.md)
