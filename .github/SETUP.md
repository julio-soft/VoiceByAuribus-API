# GitHub Actions Setup

This repository uses GitHub Actions to build AMD64 Docker images and push them to AWS ECR.

## Required GitHub Secrets

To enable the workflow, configure the following secrets in your GitHub repository:

**Settings → Secrets and variables → Actions → New repository secret**

| Secret Name | Description | Example |
|-------------|-------------|---------|
| `AWS_ACCESS_KEY_ID` | AWS IAM access key ID with ECR permissions | `AKIAIOSFODNN7EXAMPLE` |
| `AWS_SECRET_ACCESS_KEY` | AWS IAM secret access key | `wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY` |

## IAM Permissions Required

The AWS credentials must have the following permissions:

```json
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
            "Resource": "*"
        }
    ]
}
```

## Workflow Triggers

The workflow runs:
- **Automatically** on every push to the `main` branch
- **Manually** via the "Actions" tab → "Build and Push to ECR" → "Run workflow"

## Image Tags

The workflow pushes two tags to ECR:
- `simplified` - Specific tag for the simplified version
- `latest` - Always points to the most recent build

## Deployment Process

1. **Push code changes** to the `main` branch
2. **GitHub Actions builds** the AMD64 Docker image
3. **Image is pushed** to ECR automatically
4. **Update App Runner** to use the new image:
   ```bash
   aws apprunner start-deployment \
     --service-arn arn:aws:apprunner:us-east-1:265584593347:service/voice-by-auribus-api/da8699f8a9f843679325b295095d2a5e \
     --region us-east-1
   ```

## Manual Build (Alternative)

If you need to build locally instead:

```bash
# Login to ECR
aws ecr get-login-password --region us-east-1 | \
  docker login --username AWS --password-stdin 265584593347.dkr.ecr.us-east-1.amazonaws.com

# Build AMD64 image (slow on ARM Mac due to emulation)
docker buildx build \
  --platform linux/amd64 \
  --file VoiceByAuribus.API/Dockerfile.apprunner \
  --tag 265584593347.dkr.ecr.us-east-1.amazonaws.com/voice-by-auribus-api:simplified \
  --push \
  .
```

## Troubleshooting

### Build fails with authentication error
- Verify AWS credentials are correctly set in GitHub secrets
- Check IAM permissions include ECR access

### Image architecture mismatch
- Workflow uses `--platform linux/amd64` explicitly
- GitHub runners are native x86_64 (no emulation needed)

### App Runner deployment fails
- Verify ECR image URI is correct
- Check App Runner service is configured for x86_64 (not ARM64)
