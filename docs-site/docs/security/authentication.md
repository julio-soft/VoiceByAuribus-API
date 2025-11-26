---
sidebar_position: 1
---

# Authentication Security

Best practices for securing your API authentication and protecting your credentials.

## Overview

VoiceByAuribus uses OAuth 2.0 Client Credentials flow for machine-to-machine authentication. This guide covers security best practices to protect your API credentials and access tokens.

## Protecting Your Credentials

### Never Expose Credentials

Your Client ID and Client Secret are sensitive and must be protected:

❌ **Don't**:
- Commit credentials to version control (Git, SVN, etc.)
- Hardcode credentials in your source code
- Share credentials via email or chat
- Include credentials in frontend JavaScript
- Log credentials in application logs

✅ **Do**:
- Store credentials in environment variables
- Use secret management services (AWS Secrets Manager, HashiCorp Vault, etc.)
- Rotate credentials periodically
- Use different credentials for development and production
- Restrict access to credentials on a need-to-know basis

### Environment Variables

Store credentials as environment variables:

**Development (.env file)**:
```bash
# .env (add to .gitignore!)
AURIBUS_CLIENT_ID=your_client_id_here
AURIBUS_CLIENT_SECRET=your_client_secret_here
```

**Node.js Example**:
```typescript
// Load from environment
const clientId = process.env.AURIBUS_CLIENT_ID;
const clientSecret = process.env.AURIBUS_CLIENT_SECRET;

if (!clientId || !clientSecret) {
  throw new Error('Missing AURIBUS credentials in environment');
}
```

**Python Example**:
```python
import os

client_id = os.getenv('AURIBUS_CLIENT_ID')
client_secret = os.getenv('AURIBUS_CLIENT_SECRET')

if not client_id or not client_secret:
    raise ValueError('Missing AURIBUS credentials in environment')
```

### Secret Management Services

For production environments, use dedicated secret management:

```typescript
// AWS Secrets Manager example
import { SecretsManagerClient, GetSecretValueCommand } from '@aws-sdk/client-secrets-manager';

async function getCredentials() {
  const client = new SecretsManagerClient({ region: 'us-east-1' });
  const response = await client.send(
    new GetSecretValueCommand({ SecretId: 'auribus/api/credentials' })
  );

  const secret = JSON.parse(response.SecretString);
  return {
    clientId: secret.client_id,
    clientSecret: secret.client_secret,
  };
}
```

## Managing Access Tokens

### Token Lifecycle

Access tokens have a **1-hour lifetime**. Implement proper token management:

```typescript
class TokenManager {
  private token: string | null = null;
  private expiresAt: number = 0;

  async getToken(): Promise<string> {
    // Check if current token is still valid
    if (this.token && Date.now() < this.expiresAt) {
      return this.token;
    }

    // Request new token
    const response = await fetch('https://auth.auribus.io/oauth2/token', {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({
        grant_type: 'client_credentials',
        client_id: process.env.AURIBUS_CLIENT_ID!,
        client_secret: process.env.AURIBUS_CLIENT_SECRET!,
        scope: 'voicebyauribus',
      }),
    });

    const data = await response.json();
    this.token = data.access_token;

    // Set expiration slightly before actual expiry (e.g., 55 minutes)
    this.expiresAt = Date.now() + (data.expires_in - 300) * 1000;

    return this.token;
  }
}
```

### Token Storage

**Server-Side Applications**:
- Store tokens in memory (not in database)
- Never log tokens
- Clear tokens on application shutdown

**DO NOT**:
- Store tokens in frontend localStorage or sessionStorage
- Include tokens in URLs or query parameters
- Cache tokens in CDN or proxy servers
- Store tokens in cookies without proper security flags

### Token Transmission

Always use HTTPS for API requests:

```typescript
// Good: HTTPS with Authorization header
const response = await fetch('https://api.auribus.io/api/v1/voices', {
  headers: {
    'Authorization': `Bearer ${token}`,
  },
});

// Bad: HTTP (insecure)
// const response = await fetch('http://api.auribus.io/...'); // DON'T DO THIS
```

## Rate Limiting

The authentication endpoint has rate limits to prevent abuse:

- **Authentication**: 10 requests per minute per IP address

### Handling Rate Limits

Implement backoff strategies:

```typescript
async function getTokenWithBackoff(retries = 3): Promise<string> {
  for (let i = 0; i < retries; i++) {
    try {
      const response = await fetch(authUrl, options);

      if (response.status === 429) {
        // Rate limited - wait before retry
        const waitTime = Math.pow(2, i) * 1000; // Exponential backoff
        await new Promise(resolve => setTimeout(resolve, waitTime));
        continue;
      }

      if (!response.ok) {
        throw new Error(`Auth failed: ${response.status}`);
      }

      const data = await response.json();
      return data.access_token;
    } catch (error) {
      if (i === retries - 1) throw error;
    }
  }

  throw new Error('Max retries exceeded');
}
```

## Security Checklist

### Development

- [ ] Credentials stored in environment variables
- [ ] `.env` file added to `.gitignore`
- [ ] No credentials in source code
- [ ] Token caching implemented
- [ ] HTTPS only for API calls
- [ ] Error logging doesn't expose tokens

### Production

- [ ] Using secret management service
- [ ] Credentials rotated periodically
- [ ] Separate credentials per environment
- [ ] Token refresh before expiration
- [ ] Network security (VPC, firewall rules)
- [ ] Monitoring for unusual authentication patterns
- [ ] Incident response plan for credential leaks

## Credential Rotation

Regularly rotate your API credentials:

1. **Request new credentials** from [support@auribus.io](mailto:support@auribus.io)
2. **Deploy new credentials** to all environments
3. **Test** with new credentials
4. **Monitor** for authentication errors
5. **Revoke old credentials** after transition period

## Monitoring and Alerts

Set up monitoring for security events:

### Authentication Failures

```typescript
async function authenticateWithMonitoring() {
  try {
    const token = await getToken();
    return token;
  } catch (error) {
    // Log security event
    logger.error('Authentication failed', {
      timestamp: new Date().toISOString(),
      error: error.message,
      // Don't log credentials!
    });

    // Alert security team
    await sendAlert('Authentication failure detected');

    throw error;
  }
}
```

### Unusual Activity

Monitor for:
- Multiple authentication failures
- Requests from unexpected IP addresses
- Unusual request patterns
- Rate limit violations

## Incident Response

If credentials are compromised:

1. **Immediately notify** [support@auribus.io](mailto:support@auribus.io)
2. **Request credential revocation**
3. **Obtain new credentials**
4. **Update all affected systems**
5. **Audit** systems for unauthorized access
6. **Review** security practices

## Common Vulnerabilities

### Avoid These Mistakes

**1. Credentials in Version Control**
```bash
# Check Git history for leaks
git log -p | grep -i "client_secret"

# If found, treat credentials as compromised and rotate immediately
```

**2. Logging Sensitive Data**
```typescript
// Bad: Logs sensitive data
console.log('Auth request:', { client_id, client_secret }); // DON'T DO THIS

// Good: Logs non-sensitive information
console.log('Auth request initiated', { timestamp: Date.now() });
```

**3. Frontend Exposure**
```typescript
// Bad: Credentials in frontend code
const token = await fetch('https://auth.auribus.io/oauth2/token', {
  body: new URLSearchParams({
    client_id: 'exposed_in_browser', // DON'T DO THIS
    client_secret: 'exposed_in_browser', // DON'T DO THIS
  }),
});

// Good: Backend handles authentication
// Frontend only receives already-issued tokens from your backend
```

## Best Practices Summary

1. **Protect Credentials**
   - Never commit to version control
   - Use environment variables or secret managers
   - Rotate periodically

2. **Manage Tokens**
   - Cache tokens in memory
   - Refresh before expiration
   - Never store in frontend

3. **Secure Transmission**
   - Always use HTTPS
   - Include token in Authorization header
   - Never in URL parameters

4. **Monitor Activity**
   - Log authentication events
   - Alert on failures
   - Audit unusual patterns

5. **Prepare for Incidents**
   - Have rotation procedure ready
   - Know who to contact
   - Document response steps

## Next Steps

- [Getting Started with Authentication](../getting-started/authentication)
- [Webhook Signature Verification](webhook-signatures)
- [Error Handling Guide](../guides/error-handling)

## Getting Help

Security concerns? Contact us immediately:

- **Security Email**: [support@auribus.io](mailto:support@auribus.io)
- **Urgent Issues**: Mark email with "SECURITY:" prefix
