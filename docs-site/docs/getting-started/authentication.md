---
sidebar_position: 2
---

# Authentication

Learn how to authenticate with the VoiceByAuribus API.

## Overview

VoiceByAuribus API uses **OAuth 2.0 Client Credentials** grant for machine-to-machine authentication.

## Authentication Flow

1. Exchange your credentials for an access token
2. Include the token in the `Authorization` header
3. Tokens expire after 1 hour

## Getting Credentials

Contact support@auribus.io to receive your API credentials.

## Obtaining an Access Token

```bash
curl -X POST https://auth.auribus.io/oauth2/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=YOUR_CLIENT_ID" \
  -d "client_secret=YOUR_CLIENT_SECRET" \
  -d "scope=voicebyauribus"
```

## Using the Access Token

Include the token in all API requests:

```bash
curl -X GET https://api.auribus.io/api/v1/voices \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

## Token Expiration

Tokens expire after 1 hour. Implement automatic token refresh in your application.
