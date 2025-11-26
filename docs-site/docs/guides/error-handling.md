---
sidebar_position: 6
---

# Error Handling

Learn how to handle errors gracefully in your VoiceByAuribus API integration.

## Overview

The VoiceByAuribus API uses standard HTTP status codes and provides detailed error messages to help you diagnose and resolve issues quickly. All errors follow a consistent JSON format for easy parsing and handling.

## Error Response Format

When an error occurs, the API returns a JSON response with the following structure:

```json
{
  "success": false,
  "message": "Human-readable error message describing what went wrong"
}
```

For validation errors, additional details are provided:

```json
{
  "success": false,
  "message": "Validation failed",
  "errors": [
    {
      "field": "file_name",
      "message": "File name is required"
    },
    {
      "field": "content_type",
      "message": "Content type must be audio/wav, audio/mp3, or audio/flac"
    }
  ]
}
```

## HTTP Status Codes

### Success Codes

| Code | Status | Description |
|------|--------|-------------|
| `200` | OK | Request succeeded |
| `201` | Created | Resource created successfully |
| `204` | No Content | Request succeeded with no response body |

### Client Error Codes (4xx)

| Code | Status | Common Causes | Solutions |
|------|--------|---------------|-----------|
| `400` | Bad Request | Invalid request body, missing required fields, invalid values | Check request format and field values |
| `401` | Unauthorized | Missing or invalid access token | Obtain a valid access token |
| `403` | Forbidden | Valid token but insufficient permissions, download URL expired | Check scopes, request fresh URLs |
| `404` | Not Found | Resource ID doesn't exist or was deleted | Verify resource ID, check if resource was deleted |
| `422` | Unprocessable Entity | Validation errors, business logic violations | Review validation errors in response |
| `429` | Too Many Requests | Rate limit exceeded | Implement backoff, reduce request frequency |

### Server Error Codes (5xx)

| Code | Status | Description | Action |
|------|--------|-------------|--------|
| `500` | Internal Server Error | Unexpected server error | Retry with exponential backoff, contact support if persists |
| `502` | Bad Gateway | Temporary service unavailability | Retry with exponential backoff |
| `503` | Service Unavailable | Service temporarily down for maintenance | Retry later, check status page |

## Common Errors

### 401 Unauthorized

**Error Message**: "Unauthorized - Invalid or missing authentication token"

**Causes**:
- Access token is missing from the request
- Access token has expired (tokens expire after 1 hour)
- Access token is malformed or invalid

**Solutions**:
```typescript
// Check if token exists
if (!token) {
  // Request new token
  token = await getAccessToken();
}

// Check if token expired (tokens expire after 1 hour)
if (Date.now() >= tokenExpiresAt) {
  token = await getAccessToken();
}

// Include token in all requests
const response = await fetch(url, {
  headers: {
    'Authorization': `Bearer ${token}`
  }
});
```

### 400 Bad Request

**Error Message**: "Bad request - Invalid parameters"

**Causes**:
- Missing required fields
- Invalid field values
- Incorrect JSON format

**Solutions**:
```typescript
// Validate required fields before sending
const validateRequest = (data) => {
  if (!data.file_name) {
    throw new Error('file_name is required');
  }
  if (!data.content_type) {
    throw new Error('content_type is required');
  }
  if (!['audio/wav', 'audio/mp3', 'audio/flac'].includes(data.content_type)) {
    throw new Error('Invalid content_type');
  }
};

// Use the validator
validateRequest(requestData);
```

### 403 Forbidden

**Error Message**: "Forbidden - Access denied" or "Download URL expired"

**Causes**:
- Pre-signed download URLs expired (valid for 12 hours)
- Upload URLs expired (valid for 15 minutes)
- Accessing resources you don't own

**Solutions**:
```typescript
// For download URLs: Get fresh URLs from the API
const response = await fetch(
  `https://api.auribus.io/api/v1/voice-conversions/${id}`,
  {
    headers: { 'Authorization': `Bearer ${token}` }
  }
);
const { data } = await response.json();
const freshUrl = data.output_url; // Valid for next 12 hours

// For upload URLs: Request a new upload URL
const uploadResponse = await fetch(
  'https://api.auribus.io/api/v1/audio-files',
  {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      file_name: 'audio.wav',
      content_type: 'audio/wav',
    }),
  }
);
```

### 404 Not Found

**Error Message**: "Voice model not found", "Audio file not found", etc.

**Causes**:
- Resource ID doesn't exist
- Resource was deleted
- Typo in resource ID

**Solutions**:
```typescript
// Verify resource exists before using
const checkResourceExists = async (resourceType, id) => {
  const response = await fetch(
    `https://api.auribus.io/api/v1/${resourceType}/${id}`,
    {
      headers: { 'Authorization': `Bearer ${token}` }
    }
  );

  if (response.status === 404) {
    throw new Error(`${resourceType} ${id} not found`);
  }

  return await response.json();
};

// Example usage
await checkResourceExists('voices', voiceId);
await checkResourceExists('audio-files', audioFileId);
```

### 422 Unprocessable Entity

**Error Message**: "Validation failed"

**Causes**:
- Field validation failures
- Business logic violations (e.g., audio file not ready, max subscriptions reached)

**Example Response**:
```json
{
  "success": false,
  "message": "Validation failed",
  "errors": [
    {
      "field": "url",
      "message": "URL must be HTTPS"
    },
    {
      "field": "events",
      "message": "At least one event must be specified"
    }
  ]
}
```

**Solutions**:
```typescript
// Parse and display validation errors
const handleValidationErrors = (response) => {
  if (response.errors && Array.isArray(response.errors)) {
    response.errors.forEach(error => {
      console.error(`${error.field}: ${error.message}`);
    });
  }
};

// Example
const response = await fetch(url, options);
const data = await response.json();

if (response.status === 422) {
  handleValidationErrors(data);
}
```

### 429 Too Many Requests

**Error Message**: "Too many requests - Rate limit exceeded"

**Causes**:
- Exceeding API rate limits
- Too many authentication requests
- Polling too frequently

**Solutions**:
```typescript
// Implement exponential backoff
const fetchWithBackoff = async (url, options, maxRetries = 3) => {
  for (let i = 0; i < maxRetries; i++) {
    const response = await fetch(url, options);

    if (response.status !== 429) {
      return response;
    }

    // Exponential backoff: 2^i seconds
    const waitTime = Math.pow(2, i) * 1000;
    console.log(`Rate limited. Waiting ${waitTime}ms before retry...`);
    await new Promise(resolve => setTimeout(resolve, waitTime));
  }

  throw new Error('Max retries exceeded');
};

// Use webhooks instead of polling
// This is the best solution for avoiding rate limits
```

### 500 Internal Server Error

**Error Message**: "An error occurred while processing your request"

**Causes**:
- Unexpected server error
- System malfunction

**Solutions**:
```typescript
// Retry with exponential backoff
const fetchWithRetry = async (url, options, maxRetries = 3) => {
  for (let i = 0; i < maxRetries; i++) {
    try {
      const response = await fetch(url, options);

      // Only retry on 5xx errors
      if (response.status < 500) {
        return response;
      }

      console.error(`Server error (${response.status}). Attempt ${i + 1}/${maxRetries}`);
    } catch (error) {
      console.error(`Request failed. Attempt ${i + 1}/${maxRetries}:`, error);
    }

    // Exponential backoff
    if (i < maxRetries - 1) {
      const waitTime = Math.pow(2, i) * 1000;
      await new Promise(resolve => setTimeout(resolve, waitTime));
    }
  }

  throw new Error('Max retries exceeded');
};
```

## Best Practices

### 1. Always Check Status Codes

```typescript
const response = await fetch(url, options);

if (!response.ok) {
  const error = await response.json();
  throw new Error(`API Error (${response.status}): ${error.message}`);
}

const data = await response.json();
```

### 2. Implement Retry Logic

Retry transient errors (5xx, 429) but not client errors (4xx):

```typescript
const shouldRetry = (statusCode: number): boolean => {
  // Retry server errors and rate limits
  return statusCode >= 500 || statusCode === 429;
};

const fetchWithRetry = async (url, options) => {
  let lastError;

  for (let attempt = 0; attempt < 3; attempt++) {
    try {
      const response = await fetch(url, options);

      if (response.ok || !shouldRetry(response.status)) {
        return response;
      }

      lastError = await response.json();
    } catch (error) {
      lastError = error;
    }

    // Wait before retrying (exponential backoff)
    await new Promise(r => setTimeout(r, Math.pow(2, attempt) * 1000));
  }

  throw lastError;
};
```

### 3. Parse Validation Errors

Display field-specific errors to users:

```typescript
interface ValidationError {
  field: string;
  message: string;
}

const displayValidationErrors = (errors: ValidationError[]) => {
  const errorMap = new Map<string, string>();

  errors.forEach(error => {
    errorMap.set(error.field, error.message);
  });

  // Display errors next to form fields
  errorMap.forEach((message, field) => {
    console.error(`${field}: ${message}`);
    // Update UI to show error next to the field
  });
};
```

### 4. Log Errors Properly

Include context for debugging:

```typescript
const logError = (context: string, error: any, requestData?: any) => {
  console.error({
    context,
    error: error.message,
    statusCode: error.statusCode,
    requestData, // Useful for debugging
    timestamp: new Date().toISOString(),
  });
};

// Usage
try {
  const response = await createConversion(data);
} catch (error) {
  logError('create_conversion', error, data);
  // Handle error appropriately
}
```

### 5. Handle Token Expiration

Automatically refresh tokens:

```typescript
class ApiClient {
  private token: string | null = null;
  private tokenExpiresAt: number = 0;

  async ensureValidToken() {
    if (!this.token || Date.now() >= this.tokenExpiresAt) {
      const response = await this.getAccessToken();
      this.token = response.access_token;
      // Tokens expire in 3600 seconds (1 hour)
      this.tokenExpiresAt = Date.now() + (response.expires_in * 1000);
    }
  }

  async fetch(url: string, options: RequestInit = {}) {
    await this.ensureValidToken();

    const response = await fetch(url, {
      ...options,
      headers: {
        ...options.headers,
        'Authorization': `Bearer ${this.token}`,
      },
    });

    // If we get 401, token might be invalidated server-side
    if (response.status === 401) {
      // Force token refresh
      this.token = null;
      await this.ensureValidToken();

      // Retry request once with new token
      return fetch(url, {
        ...options,
        headers: {
          ...options.headers,
          'Authorization': `Bearer ${this.token}`,
        },
      });
    }

    return response;
  }
}
```

## Putting It All Together

Combine the best practices into a simple, robust pattern:

```typescript
async function makeApiRequest(url: string, options: RequestInit = {}) {
  // Ensure valid token
  const token = await getValidToken();

  // Make request with proper error handling
  const response = await fetch(url, {
    ...options,
    headers: {
      ...options.headers,
      'Authorization': `Bearer ${token}`,
    },
  });

  // Handle errors
  if (!response.ok) {
    const error = await response.json();

    // Retry on rate limit or server error
    if (response.status === 429 || response.status >= 500) {
      // Implement retry with backoff (see Best Practices #2)
      throw new RetryableError(error.message);
    }

    // Client errors - don't retry
    throw new ApiError(error.message, response.status);
  }

  return await response.json();
}
```

This simple pattern covers the essential error handling needs. Refer to the individual best practices sections above for detailed implementations of token management, retry logic, and validation error handling.

## Next Steps

- **[Quickstart Guide](../getting-started/quickstart)**: Complete API workflow example
- **[Authentication](../getting-started/authentication)**: Learn about token management
- **[API Reference](../api/voicebyauribus-api)**: View all endpoint specifications

## Getting Help

Encountering persistent errors? We're here to help:

- **Email**: [support@auribus.io](mailto:support@auribus.io)
- **Include**: Error messages, request details, and steps to reproduce
