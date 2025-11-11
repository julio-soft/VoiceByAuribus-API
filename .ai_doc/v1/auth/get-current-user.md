# GET /api/v1/auth/current-user

Returns information about the currently authenticated principal.

## Authentication

- Requires JWT issued by AWS Cognito user pool `us-east-1_2GQIgX9Vw`.
- Required scopes: `voice-by-auribus-api/base` or `voice-by-auribus-api/admin`.

## Responses

### 200 OK

```json
{
  "user_id": "00000000-0000-0000-0000-000000000000",
  "username": "string",
  "email": "user@example.com",
  "scopes": ["voice-by-auribus-api/base"],
  "is_admin": false
}
```

### 401 Unauthorized / 403 Forbidden

Returned when the token is missing, expired, or the caller lacks the required scope.
