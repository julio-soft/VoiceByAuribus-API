# AWS Secrets Manager - Secrets Structure

This document describes the structure of secrets stored in AWS Secrets Manager for VoiceByAuribus API.

## Secret Naming Convention

Secrets follow this naming pattern:
```
voice-by-auribus-api/{environment}
```

### Environments

- **Development**: `voice-by-auribus-api/development` (optional)
- **Staging**: `voice-by-auribus-api/staging` (required)
- **Production**: `voice-by-auribus-api/production` (required)

---

## Secret Structure (JSON Format)

Create a secret in AWS Secrets Manager with the following JSON structure:

```json
{
  "ConnectionStrings__DefaultConnection": "Host=your-db-host.rds.amazonaws.com;Port=5432;Database=voicebyauribus;Username=dbuser;Password=your-secure-db-password",

  "Authentication__Cognito__Region": "us-east-1",
  "Authentication__Cognito__UserPoolId": "us-east-1_XXXXXXXXX",
  "Authentication__Cognito__Audience": "voicebyauribus-api",

  "AWS__S3__AudioBucketName": "voicebyauribus-audio-files-production",
  "AWS__S3__Region": "us-east-1",

  "AWS__SQS__PreprocessingQueueUrl": "https://sqs.us-east-1.amazonaws.com/123456789012/preprocessing-queue",
  "AWS__SQS__Region": "us-east-1"
}
```

---

## Configuration Key Mapping

The double underscore (`__`) in secret keys is automatically converted to `:` for ASP.NET Core configuration hierarchy.

| Secret Key | Configuration Access | Description |
|------------|---------------------|-------------|
| `ConnectionStrings__DefaultConnection` | `GetConnectionString("DefaultConnection")` | PostgreSQL connection string |
| `Authentication__Cognito__Region` | `["Authentication:Cognito:Region"]` | AWS Cognito region |
| `Authentication__Cognito__UserPoolId` | `["Authentication:Cognito:UserPoolId"]` | Cognito User Pool ID |
| `Authentication__Cognito__Audience` | `["Authentication:Cognito:Audience"]` | API resource server identifier |
| `AWS__S3__AudioBucketName` | `["AWS:S3:AudioBucketName"]` | S3 bucket for audio files |
| `AWS__S3__Region` | `["AWS:S3:Region"]` | S3 region |
| `AWS__SQS__PreprocessingQueueUrl` | `["AWS:SQS:PreprocessingQueueUrl"]` | SQS queue URL |
| `AWS__SQS__Region` | `["AWS:SQS:Region"]` | SQS region |

---

## How to Create Secrets in AWS Secrets Manager

### Using AWS Console

1. Go to [AWS Secrets Manager Console](https://console.aws.amazon.com/secretsmanager)
2. Click **Store a new secret**
3. Select **Other type of secret**
4. Choose **Plaintext** tab
5. Paste the JSON structure above (update with your values)
6. Click **Next**
7. Enter secret name: `voice-by-auribus-api/production`
8. Click **Next** through the remaining steps

### Using AWS CLI

```bash
# Create production secret
aws secretsmanager create-secret \
  --name voice-by-auribus-api/production \
  --description "VoiceByAuribus API Production Configuration" \
  --secret-string file://production-secret.json \
  --region us-east-1

# Update existing secret
aws secretsmanager update-secret \
  --secret-id voice-by-auribus-api/production \
  --secret-string file://production-secret.json \
  --region us-east-1
```

### Using Terraform

```hcl
resource "aws_secretsmanager_secret" "voicebyauribus_production" {
  name        = "voice-by-auribus-api/production"
  description = "VoiceByAuribus API Production Configuration"

  tags = {
    Environment = "production"
    Application = "voicebyauribus-api"
  }
}

resource "aws_secretsmanager_secret_version" "voicebyauribus_production" {
  secret_id = aws_secretsmanager_secret.voicebyauribus_production.id

  secret_string = jsonencode({
    "ConnectionStrings__DefaultConnection" = "Host=${aws_db_instance.postgres.endpoint};Port=5432;Database=voicebyauribus;Username=${var.db_username};Password=${var.db_password}"
    "Authentication__Cognito__Region"      = var.cognito_region
    "Authentication__Cognito__UserPoolId"  = aws_cognito_user_pool.main.id
    "Authentication__Cognito__Audience"    = "voicebyauribus-api"
    "AWS__S3__AudioBucketName"             = aws_s3_bucket.audio_files.id
    "AWS__S3__Region"                      = var.aws_region
    "AWS__SQS__PreprocessingQueueUrl"      = aws_sqs_queue.preprocessing.url
    "AWS__SQS__Region"                     = var.aws_region
  })
}
```

---

## Environment-Specific Examples

### Development Secret (`voice-by-auribus-api/development`)

```json
{
  "ConnectionStrings__DefaultConnection": "Host=localhost;Port=5432;Database=voicebyauribus_dev;Username=postgres;Password=dev123",

  "Authentication__Cognito__Region": "us-east-1",
  "Authentication__Cognito__UserPoolId": "us-east-1_DevPoolID",
  "Authentication__Cognito__Audience": "voicebyauribus-api-dev"
}
```

> **Note**: Development secrets are **optional**. If not found, the app will use values from `appsettings.Development.json`.

### Staging Secret (`voice-by-auribus-api/staging`)

```json
{
  "ConnectionStrings__DefaultConnection": "Host=staging-db.example.com;Port=5432;Database=voicebyauribus;Username=staginguser;Password=staging-secure-password",

  "Authentication__Cognito__Region": "us-east-1",
  "Authentication__Cognito__UserPoolId": "us-east-1_StagingPool",
  "Authentication__Cognito__Audience": "voicebyauribus-api",

  "AWS__S3__AudioBucketName": "voicebyauribus-audio-staging",
  "AWS__S3__Region": "us-east-1",

  "AWS__SQS__PreprocessingQueueUrl": "https://sqs.us-east-1.amazonaws.com/123456789012/preprocessing-queue-staging",
  "AWS__SQS__Region": "us-east-1"
}
```

### Production Secret (`voice-by-auribus-api/production`)

```json
{
  "ConnectionStrings__DefaultConnection": "Host=prod-db.cluster-xxxxx.us-east-1.rds.amazonaws.com;Port=5432;Database=voicebyauribus;Username=produser;Password=super-secure-prod-password",

  "Authentication__Cognito__Region": "us-east-1",
  "Authentication__Cognito__UserPoolId": "us-east-1_ProdPoolXX",
  "Authentication__Cognito__Audience": "voicebyauribus-api",

  "AWS__S3__AudioBucketName": "voicebyauribus-audio-production",
  "AWS__S3__Region": "us-east-1",

  "AWS__SQS__PreprocessingQueueUrl": "https://sqs.us-east-1.amazonaws.com/123456789012/preprocessing-queue-prod",
  "AWS__SQS__Region": "us-east-1"
}
```

---

## IAM Permissions Required

The application's IAM role (EC2, ECS, Lambda, App Runner) needs these permissions:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "secretsmanager:GetSecretValue",
        "secretsmanager:DescribeSecret"
      ],
      "Resource": [
        "arn:aws:secretsmanager:us-east-1:123456789012:secret:voice-by-auribus-api/*"
      ]
    }
  ]
}
```

### Example IAM Policy (Terraform)

```hcl
resource "aws_iam_policy" "secrets_access" {
  name        = "voicebyauribus-secrets-access"
  description = "Allow VoiceByAuribus API to read secrets"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "secretsmanager:GetSecretValue",
          "secretsmanager:DescribeSecret"
        ]
        Resource = "arn:aws:secretsmanager:${var.aws_region}:${data.aws_caller_identity.current.account_id}:secret:voice-by-auribus-api/*"
      }
    ]
  })
}

# Attach to App Runner service role
resource "aws_iam_role_policy_attachment" "apprunner_secrets" {
  role       = aws_iam_role.apprunner_instance_role.name
  policy_arn = aws_iam_policy.secrets_access.arn
}
```

---

## Configuration Precedence

Configuration values are loaded in this order (later sources override earlier ones):

1. **appsettings.json** (lowest priority)
2. **appsettings.{Environment}.json**
3. **AWS Secrets Manager** ← Overrides appsettings
4. **Environment Variables** (highest priority)

Example:
```
appsettings.json → appsettings.Production.json → AWS Secrets Manager → Env Vars
```

---

## Troubleshooting

### Secret not found error

```
ResourceNotFoundException: Secrets Manager can't find the specified secret.
```

**Solution**: Ensure the secret exists with the exact name:
```bash
aws secretsmanager describe-secret \
  --secret-id voice-by-auribus-api/production \
  --region us-east-1
```

### Access denied error

```
AccessDeniedException: User is not authorized to perform: secretsmanager:GetSecretValue
```

**Solution**: Add the IAM policy shown above to the application's IAM role.

### Configuration values not loading

1. Check the environment name matches:
   ```bash
   echo $ASPNETCORE_ENVIRONMENT  # Should be "Production", "Staging", or "Development"
   ```

2. Verify secret keys use double underscore (`__`):
   ```json
   ✅ "Authentication__Cognito__Region": "us-east-1"
   ❌ "Authentication:Cognito:Region": "us-east-1"
   ```

3. Check application logs:
   ```
   [Secrets Manager] Loading secrets from: voice-by-auribus-api/production
   ```

---

## Security Best Practices

1. ✅ **Never commit secrets to Git** - Use Secrets Manager instead
2. ✅ **Rotate secrets regularly** - Use AWS Secrets Manager rotation
3. ✅ **Use separate secrets per environment** - Don't share production secrets
4. ✅ **Enable secret versioning** - AWS Secrets Manager does this automatically
5. ✅ **Use least privilege IAM** - Only grant access to necessary secrets
6. ✅ **Enable CloudTrail logging** - Monitor secret access
7. ✅ **Use encryption at rest** - AWS Secrets Manager encrypts with KMS by default

---

## Testing Locally

For local development, you can test with AWS credentials:

1. Configure AWS CLI:
   ```bash
   aws configure --profile voicebyauribus
   ```

2. Set environment:
   ```bash
   export ASPNETCORE_ENVIRONMENT=Development
   export AWS_PROFILE=voicebyauribus
   ```

3. Run the application:
   ```bash
   dotnet run
   ```

The app will attempt to load `voice-by-auribus-api/development` (optional), falling back to `appsettings.Development.json` if not found.

---

## Additional Resources

- [AWS Secrets Manager Documentation](https://docs.aws.amazon.com/secretsmanager/)
- [ASP.NET Core Configuration](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/)
- Custom Configuration Provider: `Shared/Infrastructure/Configuration/README.md`
