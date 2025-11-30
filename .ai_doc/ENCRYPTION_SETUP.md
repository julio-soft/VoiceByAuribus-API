# Webhook Secret Encryption Setup

## Overview

Webhook secrets are encrypted using AES-256-GCM encryption with a master key stored in AWS Secrets Manager. This allows the system to:
- Store secrets securely (encrypted at rest)
- Retrieve secrets for HMAC signing (decrypt when needed)
- Rotate the master key if compromised

## Master Key Generation

Generate a cryptographically secure 32-byte master key:

```bash
# Generate base64-encoded 32-byte key
openssl rand -base64 32
```

Example output:
```
7H9x2K4mP8nQ5vR6tW3yZ1aB4cD7eF9gH2jK5mN8pR==
```

**IMPORTANT**: Save this key securely. If lost, all encrypted secrets become unrecoverable.

## AWS Secrets Manager Configuration

### Production Secret

Add the encryption master key to your existing production secret in AWS Secrets Manager.

**Secret Name**: `voice-by-auribus-api/production`

**JSON Structure** (secrets only, using double-underscore notation for .NET configuration binding):

```json
{
  "ConnectionStrings__DefaultConnection": "Host=...;Port=5432;Database=...;Username=...;Password=...;SslMode=Require",
  "Webhooks__ApiKey": "your-webhook-api-key-for-preprocessing-callbacks",
  "Encryption__MasterKey": "base64-encoded-32-byte-key-here"
}
```

> **Note**: Only actual secrets are stored in AWS Secrets Manager. Configuration values (Cognito settings, S3 bucket names, SQS queue names, etc.) are stored in `appsettings.json` since they are not sensitive.

### Using AWS CLI

```bash
# 1. Generate master key
MASTER_KEY=$(openssl rand -base64 32)
echo "Generated master key: $MASTER_KEY"

# 2. Get existing secret value
EXISTING_SECRET=$(aws secretsmanager get-secret-value \
  --secret-id voice-by-auribus-api/production \
  --region us-east-1 \
  --query SecretString \
  --output text)

# 3. Add Encryption__MasterKey to the JSON (double-underscore format)
# Use jq to merge the new key
UPDATED_SECRET=$(echo "$EXISTING_SECRET" | jq --arg key "$MASTER_KEY" \
  '. + {Encryption__MasterKey: $key}')

# 4. Update the secret
aws secretsmanager update-secret \
  --secret-id voice-by-auribus-api/production \
  --region us-east-1 \
  --secret-string "$UPDATED_SECRET"

echo "Secret updated successfully!"
```

### Using AWS Console

1. Go to AWS Secrets Manager console
2. Navigate to `voice-by-auribus-api/production`
3. Click "Retrieve secret value"
4. Click "Edit"
5. Add the `Encryption__MasterKey` field (double-underscore notation):
   ```json
   "Encryption__MasterKey": "YOUR_GENERATED_KEY_HERE"
   ```
6. Click "Save"

## Development/Staging Secrets

For development and staging environments, create separate secrets:

**Development**: `voice-by-auribus-api/development` (optional)
**Staging**: `voice-by-auribus-api/staging`

Each environment should have its own unique master key for security isolation.

## Local Development

For local development without AWS Secrets Manager, add the key to `appsettings.json`:

```json
{
  "Encryption": {
    "MasterKey": "LOCAL_DEV_KEY_DO_NOT_USE_IN_PRODUCTION"
  }
}
```

**WARNING**: Never commit production master keys to version control!

## Security Best Practices

### Key Rotation

If the master key is compromised:

1. **Generate new master key**:
   ```bash
   openssl rand -base64 32
   ```

2. **Create migration script** to re-encrypt all existing secrets:
   ```sql
   -- This requires application-level code to:
   -- 1. Decrypt with old key
   -- 2. Re-encrypt with new key
   -- 3. Update database
   ```

3. **Deploy new key** to AWS Secrets Manager

4. **Run re-encryption script** before deploying new code

5. **Deploy application** with new key

### Access Control

Restrict IAM permissions for Secrets Manager:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "secretsmanager:GetSecretValue"
      ],
      "Resource": "arn:aws:secretsmanager:us-east-1:ACCOUNT_ID:secret:voice-by-auribus-api/*"
    }
  ]
}
```

The application only needs `GetSecretValue` permission, not `PutSecretValue` or `DeleteSecret`.

### Monitoring

Enable CloudWatch alarms for:
- Failed secret retrieval attempts
- Encryption/decryption failures
- Unusual access patterns

## Encryption Details

### Algorithm
- **AES-256-GCM** (Galois/Counter Mode)
- **Nonce**: 12 bytes (96 bits), randomly generated per encryption
- **Tag**: 16 bytes (128 bits) for authentication
- **Key**: 32 bytes (256 bits) from AWS Secrets Manager

### Format

Encrypted secrets are stored in the database as:
```
{base64(nonce)}:{base64(ciphertext)}:{base64(tag)}
```

Example:
```
rX9kL2mP5vR8tW==:aB4cD7eF9gH2jK5mN8pR3sT6uX9yZ==:1aB4cD7eF9gH2jK5mN8pR3sT
```

### Benefits

- **Authenticated Encryption**: GCM mode provides both confidentiality and authenticity
- **Unique Nonces**: Each encryption uses a random nonce, preventing pattern analysis
- **Fast**: Hardware-accelerated AES-NI on modern CPUs
- **Standard**: NIST-approved algorithm (FIPS 140-2)

## Troubleshooting

### Error: "Encryption master key not configured"

**Cause**: `Encryption:MasterKey` not found in configuration.

**Solution**: Add master key to AWS Secrets Manager or appsettings.json.

### Error: "Invalid master key length"

**Cause**: Master key is not exactly 32 bytes when decoded.

**Solution**: Regenerate key using `openssl rand -base64 32`.

### Error: "Decryption failed"

**Cause**: Data was encrypted with a different key, or data is corrupted.

**Solution**:
- Verify the correct master key is configured
- Check if key was rotated without migrating data
- For corrupted data, regenerate the webhook secret

### Error: "Invalid encrypted text format"

**Cause**: Database value doesn't match expected format `nonce:ciphertext:tag`.

**Cause**: Old data still using BCrypt hashing.

**Solution**: User must regenerate their webhook secret via the API.

## Migration from BCrypt

The system was migrated from BCrypt hashing (one-way) to AES-GCM encryption (reversible).

**Migration Path**:
1. Existing webhook subscriptions created before migration: **Users must regenerate secrets**
2. New subscriptions created after migration: **Automatically encrypted**

**User Action Required**:
```bash
# User must call the regenerate-secret endpoint
curl -X POST https://api.voicebyauribus.com/api/v1/webhooks/subscriptions/{id}/regenerate-secret \
  -H "Authorization: Bearer $TOKEN"
```

This returns a new secret encrypted with AES-256-GCM.

## Compliance

- **GDPR**: Encryption at rest for user secrets
- **SOC 2**: Secure key management with AWS Secrets Manager
- **FIPS 140-2**: AES-256-GCM is NIST-approved
- **PCI DSS**: Strong cryptography for sensitive data

## References

- [AWS Secrets Manager Best Practices](https://docs.aws.amazon.com/secretsmanager/latest/userguide/best-practices.html)
- [NIST AES-GCM Recommendation](https://csrc.nist.gov/publications/detail/sp/800-38d/final)
- [.NET AesGcm Documentation](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aesgcm)
