#!/bin/bash

# Script to add encryption master key to AWS Secrets Manager
# Usage: ./add-encryption-key-to-aws-secret.sh [environment]
# Example: ./add-encryption-key-to-aws-secret.sh production

set -e  # Exit on error

# Configuration
ENVIRONMENT=${1:-production}
SECRET_NAME="voice-by-auribus-api/${ENVIRONMENT}"
AWS_REGION="us-east-1"

echo "=================================================="
echo "Add Encryption Master Key to AWS Secrets Manager"
echo "=================================================="
echo ""
echo "Environment: $ENVIRONMENT"
echo "Secret Name: $SECRET_NAME"
echo "AWS Region: $AWS_REGION"
echo ""

# Check if AWS CLI is installed
if ! command -v aws &> /dev/null; then
    echo "ERROR: AWS CLI is not installed"
    echo "Install with: brew install awscli"
    exit 1
fi

# Check if jq is installed
if ! command -v jq &> /dev/null; then
    echo "ERROR: jq is not installed"
    echo "Install with: brew install jq"
    exit 1
fi

# Check if openssl is available
if ! command -v openssl &> /dev/null; then
    echo "ERROR: openssl is not installed"
    exit 1
fi

# Confirm action
echo "This script will:"
echo "1. Generate a new AES-256 master key (32 bytes, base64-encoded)"
echo "2. Fetch the existing secret from AWS Secrets Manager"
echo "3. Add 'Encryption.MasterKey' to the JSON"
echo "4. Update the secret in AWS Secrets Manager"
echo ""
read -p "Continue? (yes/no): " CONFIRM

if [ "$CONFIRM" != "yes" ]; then
    echo "Aborted."
    exit 0
fi

echo ""
echo "Step 1: Generating master key..."
MASTER_KEY=$(openssl rand -base64 32)
echo "✓ Master key generated: ${MASTER_KEY:0:20}... (truncated for security)"
echo ""
echo "IMPORTANT: Save this key securely!"
echo "Full key: $MASTER_KEY"
echo ""
read -p "Press Enter to continue..."

echo ""
echo "Step 2: Fetching existing secret from AWS..."
EXISTING_SECRET=$(aws secretsmanager get-secret-value \
  --secret-id "$SECRET_NAME" \
  --region "$AWS_REGION" \
  --query SecretString \
  --output text 2>/dev/null)

if [ $? -ne 0 ] || [ -z "$EXISTING_SECRET" ]; then
    echo "ERROR: Failed to fetch secret '$SECRET_NAME'"
    echo "Make sure:"
    echo "  1. The secret exists in AWS Secrets Manager"
    echo "  2. You have permission to read it"
    echo "  3. AWS CLI is configured with correct credentials"
    exit 1
fi

echo "✓ Secret fetched successfully"
echo ""

# Check if Encryption.MasterKey already exists
HAS_ENCRYPTION=$(echo "$EXISTING_SECRET" | jq 'has("Encryption")' 2>/dev/null || echo "false")

if [ "$HAS_ENCRYPTION" = "true" ]; then
    echo "WARNING: Secret already has 'Encryption' section!"
    echo ""
    echo "Current Encryption section:"
    echo "$EXISTING_SECRET" | jq '.Encryption'
    echo ""
    read -p "Overwrite existing encryption key? (yes/no): " OVERWRITE

    if [ "$OVERWRITE" != "yes" ]; then
        echo "Aborted. Existing encryption key preserved."
        exit 0
    fi
fi

echo "Step 3: Adding encryption master key to JSON..."
UPDATED_SECRET=$(echo "$EXISTING_SECRET" | jq --arg key "$MASTER_KEY" \
  '. + {Encryption: {MasterKey: $key}}')

if [ $? -ne 0 ]; then
    echo "ERROR: Failed to update JSON. Secret may have invalid JSON format."
    exit 1
fi

echo "✓ JSON updated successfully"
echo ""
echo "New secret structure (without sensitive values):"
echo "$UPDATED_SECRET" | jq 'del(.ConnectionStrings, .Webhooks.ApiKey, .Encryption.MasterKey) + {Encryption: {MasterKey: "[REDACTED]"}}'
echo ""

read -p "Update secret in AWS? (yes/no): " UPDATE_CONFIRM

if [ "$UPDATE_CONFIRM" != "yes" ]; then
    echo "Aborted. Secret not updated in AWS."
    exit 0
fi

echo ""
echo "Step 4: Updating secret in AWS Secrets Manager..."
aws secretsmanager update-secret \
  --secret-id "$SECRET_NAME" \
  --region "$AWS_REGION" \
  --secret-string "$UPDATED_SECRET" \
  --output json > /dev/null

if [ $? -ne 0 ]; then
    echo "ERROR: Failed to update secret in AWS"
    exit 1
fi

echo "✓ Secret updated successfully in AWS Secrets Manager!"
echo ""
echo "=================================================="
echo "✓ COMPLETED SUCCESSFULLY"
echo "=================================================="
echo ""
echo "Master Key (save this securely!):"
echo "$MASTER_KEY"
echo ""
echo "Next steps:"
echo "1. Save the master key in your password manager"
echo "2. Deploy the updated application"
echo "3. Test webhook creation and delivery"
echo ""
echo "If you need to rotate the key in the future:"
echo "1. Run this script again to generate a new key"
echo "2. Existing webhook subscriptions will need to be regenerated"
echo ""
