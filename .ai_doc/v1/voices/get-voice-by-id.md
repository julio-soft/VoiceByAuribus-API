# GET /api/v1/voices/{id}

Fetches details for a single voice model.

## Authentication

- Requires JWT issued by AWS Cognito user pool `us-east-1_2GQIgX9Vw`.
- Required scopes: `voice-by-auribus-api/base` or `voice-by-auribus-api/admin`.

## Path Parameters

- `id` (`uuid`): Identifier of the voice model to retrieve.

## Responses

### 200 OK

```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "name": "string",
  "tags": ["string"],
  "image_url": "https://...",
  "song_url": "https://...",
  "voice_model_index_path": "string | null (admin only)",
  "voice_model_path": "string | null (admin only)"
}
```

- `image_url` and `song_url` are pre-signed links valid for 12 hours.
- Internal paths are populated only for callers with the admin scope.

### 404 Not Found

Returned when the requested voice model does not exist or is filtered from the caller's context.

### 401 Unauthorized / 403 Forbidden

Returned when the token is missing, expired, or the caller lacks the required scope.
