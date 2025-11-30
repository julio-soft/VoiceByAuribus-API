# AWS Deployment Guide

**VoiceByAuribus API - AWS App Runner Deployment**

This guide provides a streamlined deployment workflow for the VoiceByAuribus API on AWS App Runner with PostgreSQL RDS, using automated CI/CD via GitHub Actions.

---

## Table of Contents

- [Architecture Overview](#architecture-overview)
- [Prerequisites](#prerequisites)
- [Deployment Methods](#deployment-methods)
- [Initial Setup](#initial-setup)
- [CI/CD with GitHub Actions](#cicd-with-github-actions)
- [Manual Deployment](#manual-deployment)
- [Environment Configuration](#environment-configuration)
- [Monitoring & Troubleshooting](#monitoring--troubleshooting)
- [Cost Optimization](#cost-optimization)

---

## Architecture Overview

### Infrastructure Stack

```
GitHub Actions (AMD64 builds) → ECR → App Runner → RDS PostgreSQL
                                  ↓
                            S3 + SQS + Secrets Manager
```

**Key Components:**

- **App Runner**: Serverless container service (AMD64 architecture required)
- **RDS PostgreSQL**: Managed database with automated backups
- **ECR**: Docker image registry
- **Secrets Manager**: Centralized secrets management
- **CloudWatch**: Logging and monitoring
- **S3**: Audio file storage
- **SQS**: Message queue for preprocessing

**Critical Architecture Decision:**

AWS App Runner only supports AMD64 (x86_64) architecture. ARM64/Graviton2 is not supported. Attempting to deploy ARM64 images will result in silent failures with no logs appearing in CloudWatch.

---

## Prerequisites

**Required Tools:**

```bash
# AWS CLI (v2.x or higher)
aws --version

# Docker (with Buildx support for AMD64 builds)
docker --version

# .NET SDK 10.0
dotnet --version
```

**AWS Configuration:**

```bash
# Configure AWS credentials
aws configure
# AWS Access Key ID: <your-key>
# AWS Secret Access Key: <your-secret>
# Default region: us-east-1
# Default output format: json

# Verify authentication
aws sts get-caller-identity
```

---

## Deployment Methods

### 1. GitHub Actions (Recommended)

**Advantages:**

- Native AMD64 builds (no emulation)
- Automated on every push to main
- Fast build times (2-5 minutes)
- Consistent environment

**How it works:**

The `.github/workflows/build-and-push.yml` workflow automatically:

1. Builds AMD64 Docker image on native runners
2. Tags with `latest` and `sha-XXXXXXX`
3. Pushes to ECR
4. Ready for deployment

**Setup:** See [CI/CD with GitHub Actions](#cicd-with-github-actions)

### 2. Manual Deployment Script

**Advantages:**

- Full control over deployment process
- Local testing before deployment
- Useful for development environments

**Limitations on Apple Silicon:**

- Docker Buildx uses QEMU emulation for AMD64 builds
- Significantly slower build times (10-30 minutes)
- May timeout on large images

**Usage:**

```bash
./scripts/deploy-to-aws.sh [version]
```

---

## Initial Setup

### 1. Create ECR Repository

```bash
export AWS_REGION=us-east-1
export AWS_ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
export PROJECT_NAME=voice-by-auribus-api

aws ecr create-repository \
  --repository-name $PROJECT_NAME \
  --region $AWS_REGION \
  --image-scanning-configuration scanOnPush=true \
  --encryption-configuration encryptionType=AES256
```

### 2. Create IAM Roles

#### ECR Access Role (for App Runner to pull images)

```bash
cat > /tmp/ecr-access-trust.json <<EOF
{
  "Version": "2012-10-17",
  "Statement": [{
    "Effect": "Allow",
    "Principal": {"Service": "build.apprunner.amazonaws.com"},
    "Action": "sts:AssumeRole"
  }]
}
EOF

aws iam create-role \
  --role-name AppRunnerECRAccessRole \
  --assume-role-policy-document file:///tmp/ecr-access-trust.json

aws iam attach-role-policy \
  --role-name AppRunnerECRAccessRole \
  --policy-arn arn:aws:iam::aws:policy/service-role/AWSAppRunnerServicePolicyForECRAccess
```

#### Instance Role (for application AWS service access)

```bash
cat > /tmp/instance-trust.json <<EOF
{
  "Version": "2012-10-17",
  "Statement": [{
    "Effect": "Allow",
    "Principal": {"Service": "tasks.apprunner.amazonaws.com"},
    "Action": "sts:AssumeRole"
  }]
}
EOF

aws iam create-role \
  --role-name VoiceByAuribusAppRunnerInstanceRole \
  --assume-role-policy-document file:///tmp/instance-trust.json

# Create inline policy with required permissions
cat > /tmp/instance-policy.json <<EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "SecretsManagerAccess",
      "Effect": "Allow",
      "Action": ["secretsmanager:GetSecretValue"],
      "Resource": "arn:aws:secretsmanager:us-east-1:$AWS_ACCOUNT_ID:secret:voice-by-auribus-api/*"
    },
    {
      "Sid": "S3BucketAccess",
      "Effect": "Allow",
      "Action": "s3:*",
      "Resource": [
        "arn:aws:s3:::voice-by-auribus-api",
        "arn:aws:s3:::voice-by-auribus-api/*"
      ]
    },
    {
      "Sid": "SQSQueuesAccess",
      "Effect": "Allow",
      "Action": "sqs:*",
      "Resource": [
        "arn:aws:sqs:us-east-1:$AWS_ACCOUNT_ID:voice-by-auribus-preprocessing.fifo",
        "arn:aws:sqs:us-east-1:$AWS_ACCOUNT_ID:aurivoice-svs-previews-nbl.fifo",
        "arn:aws:sqs:us-east-1:$AWS_ACCOUNT_ID:aurivoice-svs-inf-g1-nbl.fifo",
        "arn:aws:sqs:us-east-1:$AWS_ACCOUNT_ID:aurivoice-svs-inf-g1-nbl-alt.fifo"
      ]
    }
  ]
}
EOF

aws iam put-role-policy \
  --role-name VoiceByAuribusAppRunnerInstanceRole \
  --policy-name VoiceByAuribusApiAccess \
  --policy-document file:///tmp/instance-policy.json
```

### 3. Configure Secrets Manager

Store **only sensitive secrets** in AWS Secrets Manager (not configuration):

```bash
cat > /tmp/production-secrets.json <<EOF
{
  "ConnectionStrings__DefaultConnection": "Host=<RDS_ENDPOINT>;Port=5432;Database=voice_by_auribus_api_db;Username=<DB_USER>;Password=<DB_PASSWORD>;SslMode=Require",
  "Webhooks__ApiKey": "<SECURE_API_KEY_FOR_PREPROCESSING_WEBHOOKS>",
  "Encryption__MasterKey": "<BASE64_32_BYTE_KEY_FOR_WEBHOOK_SECRET_ENCRYPTION>"
}
EOF

aws secretsmanager create-secret \
  --name voice-by-auribus-api/production \
  --description "VoiceByAuribus API Production Secrets" \
  --secret-string file:///tmp/production-secrets.json \
  --region $AWS_REGION

rm /tmp/production-secrets.json
```

> **Note**: Only actual secrets are stored here. Configuration values (Cognito settings, S3 bucket names, SQS queue names, etc.) are stored in `appsettings.json` since they are not sensitive and don't require secret rotation.

**Generate Encryption MasterKey:**
```bash
openssl rand -base64 32
```

**Important:** Replace placeholders with actual values.

### 4. Create App Runner Service

```bash
ACCESS_ROLE_ARN=$(aws iam get-role --role-name AppRunnerECRAccessRole --query 'Role.Arn' --output text)
INSTANCE_ROLE_ARN=$(aws iam get-role --role-name VoiceByAuribusAppRunnerInstanceRole --query 'Role.Arn' --output text)
IMAGE_URI="$AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com/$PROJECT_NAME:latest"

aws apprunner create-service \
  --service-name $PROJECT_NAME \
  --source-configuration "{
    \"ImageRepository\": {
      \"ImageIdentifier\": \"$IMAGE_URI\",
      \"ImageConfiguration\": {
        \"Port\": \"8080\",
        \"RuntimeEnvironmentVariables\": {
          \"ASPNETCORE_ENVIRONMENT\": \"Production\"
        }
      },
      \"ImageRepositoryType\": \"ECR\"
    },
    \"AuthenticationConfiguration\": {
      \"AccessRoleArn\": \"$ACCESS_ROLE_ARN\"
    },
    \"AutoDeploymentsEnabled\": false
  }" \
  --instance-configuration "{
    \"Cpu\": \"1 vCPU\",
    \"Memory\": \"2 GB\",
    \"InstanceRoleArn\": \"$INSTANCE_ROLE_ARN\"
  }" \
  --health-check-configuration "{
    \"Protocol\": \"HTTP\",
    \"Path\": \"/api/v1/health\",
    \"Interval\": 10,
    \"Timeout\": 5,
    \"HealthyThreshold\": 1,
    \"UnhealthyThreshold\": 5
  }" \
  --region $AWS_REGION

# Wait for service to be ready
SERVICE_ARN=$(aws apprunner list-services \
  --query "ServiceSummaryList[?ServiceName=='$PROJECT_NAME'].ServiceArn" \
  --output text \
  --region $AWS_REGION)

aws apprunner wait service-running \
  --service-arn $SERVICE_ARN \
  --region $AWS_REGION

# Get service URL
SERVICE_URL=$(aws apprunner describe-service \
  --service-arn $SERVICE_ARN \
  --query 'Service.ServiceUrl' \
  --output text)

echo "Service deployed: https://$SERVICE_URL"
echo "Health check: https://$SERVICE_URL/api/v1/health"
```

---

## CI/CD with GitHub Actions

### Setup GitHub Secrets

Create an IAM user for GitHub Actions:

```bash
# Create IAM user
aws iam create-user --user-name github-actions-ecr-voicebyauribusapi

# Create and attach ECR policy
cat > /tmp/github-ecr-policy.json <<EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "ecr:GetAuthorizationToken",
        "ecr:BatchCheckLayerAvailability",
        "ecr:GetDownloadUrlForLayer",
        "ecr:BatchGetImage",
        "ecr:PutImage",
        "ecr:InitiateLayerUpload",
        "ecr:UploadLayerPart",
        "ecr:CompleteLayerUpload"
      ],
      "Resource": "arn:aws:ecr:us-east-1:$AWS_ACCOUNT_ID:repository/$PROJECT_NAME"
    },
    {
      "Effect": "Allow",
      "Action": "ecr:GetAuthorizationToken",
      "Resource": "*"
    }
  ]
}
EOF

aws iam create-policy \
  --policy-name GitHubActions-ECR-VoiceByAuribusApi \
  --policy-document file:///tmp/github-ecr-policy.json

aws iam attach-user-policy \
  --user-name github-actions-ecr-voicebyauribusapi \
  --policy-arn arn:aws:iam::$AWS_ACCOUNT_ID:policy/GitHubActions-ECR-VoiceByAuribusApi

# Create access keys
aws iam create-access-key --user-name github-actions-ecr-voicebyauribusapi
```

**Add secrets to GitHub repository:**

1. Go to repository Settings → Secrets and variables → Actions
2. Add the following secrets:
   - `AWS_ACCESS_KEY_ID`: From the access key creation output
   - `AWS_SECRET_ACCESS_KEY`: From the access key creation output

### Workflow Configuration

The workflow at `.github/workflows/build-and-push.yml` automatically:

- Triggers on push to `main` branch or manual dispatch
- Builds AMD64 Docker image using native Ubuntu runners
- Tags with `latest` and `sha-XXXXXXX` (short commit SHA)
- Pushes to ECR

**After workflow completes, trigger deployment:**

```bash
aws apprunner start-deployment \
  --service-arn $SERVICE_ARN \
  --region $AWS_REGION
```

---

## Manual Deployment

For local development or when GitHub Actions is not available:

```bash
# Navigate to project root
cd /path/to/VoiceByAuribus-API

# Run deployment script
chmod +x scripts/deploy-to-aws.sh
./scripts/deploy-to-aws.sh

# Or specify a version tag
./scripts/deploy-to-aws.sh v1.2.3
```

**What the script does:**

1. Validates prerequisites (AWS CLI, Docker, credentials)
2. Authenticates Docker with ECR
3. Builds AMD64 image using Docker Buildx
4. Tags with version and latest
5. Pushes to ECR
6. Triggers App Runner deployment
7. Waits for deployment completion
8. Verifies health check

**Note:** On Apple Silicon (M1/M2/M3), local builds use QEMU emulation and may be slow. GitHub Actions is recommended for production deployments.

---

## Environment Configuration

### Application Settings

Configuration is loaded from:

1. `appsettings.json` (all configuration including non-sensitive values)
2. AWS Secrets Manager (only secrets that override appsettings.json)

**Secrets stored in AWS Secrets Manager (production):**

```
ConnectionStrings__DefaultConnection  → Database connection with password
Webhooks__ApiKey                      → API key for preprocessing webhooks
Encryption__MasterKey                 → Master key for webhook secret encryption
```

**Configuration in appsettings.json (not sensitive):**

```
Authentication:Cognito:Region
Authentication:Cognito:UserPoolId
Authentication:Cognito:Audience
AWS:S3:AudioFilesBucket
AWS:S3:UploadUrlExpirationMinutes
AWS:S3:MaxFileSizeMB
AWS:SQS:AudioPreprocessingQueue
AWS:SQS:PreviewInferenceQueue
AWS:SQS:MainInferenceQueue
AWS:SQS:AltInferenceQueue
```

**Secrets Manager key conversion:**

The application converts double underscores to colons:

```
ConnectionStrings__DefaultConnection → ConnectionStrings:DefaultConnection
Webhooks__ApiKey → Webhooks:ApiKey
Encryption__MasterKey → Encryption:MasterKey
```

See `VoiceByAuribus.API/Shared/Infrastructure/Configuration/AwsSecretsManagerConfigurationProvider.cs:40`

### Health Check Configuration

**Endpoint:** `/api/v1/health`

**Implementation:** `VoiceByAuribus.API/Shared/Presentation/Controllers/HealthController.cs:28`

**Critical Implementation Detail:**

The health check service uses resource-specific AWS operations to validate service availability:

- **S3**: `GetBucketLocationAsync(bucketName)` - Requires only `s3:GetBucketLocation` permission for specific bucket
- **SQS**: `GetQueueUrlAsync(queueName)` - Requires only `sqs:GetQueueUrl` permission for specific queue

**Why not use global operations:**

Global operations like `ListBucketsAsync()` or `ListQueuesAsync()` require permissions for ALL resources, violating least privilege principle. The resource-specific approach:

- Adheres to least privilege IAM principles
- Only checks resources the application actually uses
- Prevents permission errors when IAM is properly scoped

See `VoiceByAuribus.API/Shared/Infrastructure/Services/HealthCheckService.cs:115` and `HealthCheckService.cs:143`

**Health Status Levels:**

- `healthy`: All services operational
- `degraded`: Database healthy, but S3/SQS unavailable (non-critical)
- `unhealthy`: Database connection failed (critical)

---

## Monitoring & Troubleshooting

### View Logs

**Application logs:**

```bash
aws logs tail \
  "/aws/apprunner/voice-by-auribus-api/<SERVICE_ID>/application" \
  --region us-east-1 \
  --follow
```

**Service logs (deployments, health checks):**

```bash
aws logs tail \
  "/aws/apprunner/voice-by-auribus-api/<SERVICE_ID>/service" \
  --region us-east-1 \
  --follow
```

**Get SERVICE_ID:**

```bash
SERVICE_ARN=$(aws apprunner list-services \
  --query "ServiceSummaryList[?ServiceName=='voice-by-auribus-api'].ServiceArn" \
  --output text)

echo $SERVICE_ARN | cut -d'/' -f3
```

### Common Issues

#### 1. No Logs Appearing in CloudWatch

**Symptom:** App Runner service appears running but no application logs

**Cause:** ARM64 image deployed to App Runner (which only supports AMD64)

**Solution:**

- Verify image architecture: `docker inspect <image> | grep Architecture`
- Rebuild for AMD64: Use GitHub Actions or `docker buildx --platform linux/amd64`
- Redeploy with correct architecture

#### 2. Health Check Failing

**Check health endpoint directly:**

```bash
curl https://<SERVICE_URL>/api/v1/health
```

**Common causes:**

- Database connection failed (check RDS security group)
- Secrets Manager permissions missing
- Invalid configuration in secrets

**Debug:**

```bash
# Check secrets are accessible
aws secretsmanager get-secret-value \
  --secret-id voice-by-auribus-api/production \
  --region us-east-1

# Verify IAM role permissions
aws iam get-role-policy \
  --role-name VoiceByAuribusAppRunnerInstanceRole \
  --policy-name VoiceByAuribusApiAccess
```

#### 3. Deployment Timeout

**Check deployment status:**

```bash
aws apprunner describe-service \
  --service-arn $SERVICE_ARN \
  --query 'Service.Status' \
  --output text
```

**Possible states:**

- `OPERATION_IN_PROGRESS`: Deployment in progress (normal)
- `RUNNING`: Deployment successful
- `CREATE_FAILED` / `UPDATE_FAILED`: Check service logs

#### 4. Permission Denied Errors

**Verify instance role has required permissions:**

```bash
# List attached policies
aws iam list-attached-role-policies \
  --role-name VoiceByAuribusAppRunnerInstanceRole

# List inline policies
aws iam list-role-policies \
  --role-name VoiceByAuribusAppRunnerInstanceRole

# Get inline policy
aws iam get-role-policy \
  --role-name VoiceByAuribusAppRunnerInstanceRole \
  --policy-name VoiceByAuribusApiAccess
```

---

## Cost Optimization

### Monthly Cost Estimates

**With AWS Free Tier (first 12 months):**

| Service | Configuration | Monthly Cost |
|---------|--------------|--------------|
| App Runner | 1 vCPU, 2GB RAM, 24/7 | ~$59 |
| RDS (db.t3.micro) | 20GB, single-AZ | $0 (750 hrs free) |
| ECR | Image storage (~5GB) | ~$0.50 |
| Secrets Manager | 1 secret | $0.40 |
| CloudWatch Logs | ~5GB/month | $0 (5GB free) |
| **Total** | | **~$60/month** |

**Without Free Tier:**

| Service | Monthly Cost |
|---------|--------------|
| App Runner | ~$59 |
| RDS (db.t3.micro) | ~$17 |
| ECR | ~$0.50 |
| Secrets Manager | $0.40 |
| CloudWatch Logs | ~$2.50 |
| **Total** | **~$79/month** |

### Optimization Strategies

1. **Development environments**: Pause App Runner when not in use
2. **RDS**: Use Aurora Serverless v2 for variable workloads
3. **Logs retention**: Reduce CloudWatch retention to 7 days for non-production
4. **Image optimization**: Use multi-stage Docker builds to reduce image size

---

## Production Checklist

Before going to production, ensure:

- [ ] Secrets Manager contains all production credentials
- [ ] RDS has automated backups enabled (7+ days retention)
- [ ] RDS has deletion protection enabled
- [ ] CloudWatch alarms configured for health check failures
- [ ] Custom domain configured with SSL certificate
- [ ] IAM roles follow least privilege principle
- [ ] ECR image scanning enabled
- [ ] App Runner configured with appropriate CPU/memory for load
- [ ] Health check endpoint returns 200 OK
- [ ] Application logs appearing in CloudWatch

---

## Additional Resources

**Related Documentation:**

- [AWS Resources](./AWS_RESOURCES.md) - AWS infrastructure overview
- [Secrets Structure](./SECRETS_STRUCTURE.md) - Secrets Manager schema
- [API Testing](./API_TESTING.md) - Testing guidelines
- [Architecture](./ARCHITECTURE.md) - Application architecture

**AWS Documentation:**

- [AWS App Runner](https://docs.aws.amazon.com/apprunner/)
- [Amazon RDS PostgreSQL](https://docs.aws.amazon.com/AmazonRDS/latest/UserGuide/CHAP_PostgreSQL.html)
- [AWS Secrets Manager](https://docs.aws.amazon.com/secretsmanager/)

---

**Last Updated:** 2025-01-17
**Version:** 3.0
**Status:** Production Ready
