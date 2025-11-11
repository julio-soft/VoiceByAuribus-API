# GET /api/v1/auth/status

Reports whether the caller is authenticated and which scopes are available.

## Authentication

- Requires JWT issued by AWS Cognito user pool `us-east-1_2GQIgX9Vw`.
- Required scopes: `voice-by-auribus-api/base` or `voice-by-auribus-api/admin`.

## Responses

### 200 OK

```json
{
  "is_authenticated": true,
  "user_id": "00000000-0000-0000-0000-000000000000",
  "is_admin": false,
  "scopes": [
    "voice-by-auribus-api/base"
  ]
}
```

### 401 Unauthorized / 403 Forbidden

Returned when the token is missing, expired, or the caller lacks the required scope.
