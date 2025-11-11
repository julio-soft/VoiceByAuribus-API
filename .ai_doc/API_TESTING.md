# API Testing Guide

## Prerequisites

1. **Start PostgreSQL** with Docker Compose:

   ```bash
   cd VoiceByAuribus.API
   docker-compose up -d postgres
   ```

2. **Apply EF Core migrations** (create them first if needed, from solution root):

   ```bash
   cd ..
   dotnet ef migrations add InitialCreate --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
   dotnet ef database update --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
   ```

3. **Configure AWS Cognito client secret** in `VoiceByAuribus.API/api-tests.http`:
   - Replace `YOUR_CLIENT_SECRET_HERE` with the actual client secret for client ID `1cgn2o0th0qh4av42jcbe4n1g6`.

## Testing Endpoints

Open `VoiceByAuribus.API/api-tests.http` in VS Code (requires REST Client extension) or use your preferred HTTP client.

### Authentication Flow

1. **Obtain Access Token**:
   - Run the "Get Access Token (M2M)" request.
   - The `@accessToken` variable will be automatically populated for subsequent requests.
   - Default scope is `voice-by-auribus-api/base`.

2. **Test Auth Endpoints**:
   - `GET /auth/current-user` - Returns current authenticated user info.
   - `GET /auth/status` - Returns authentication status and scopes.

3. **Test Voice Endpoints**:
   - `GET /voices` - Lists all available voice models (with pre-signed S3 URLs).
   - `GET /voices/{id}` - Get specific voice model details.

### Admin Testing

To test admin-only features (internal model paths):

1. Update `@scope` variable to `voice-by-auribus-api/admin`.
2. Re-run the "Get Access Token" request.
3. Admin users will see `voice_model_index_path` and `voice_model_path` fields populated in responses.

## Token Management

- **Access tokens expire** based on Cognito configuration (check `@expiresIn` variable).
- **M2M tokens don't support refresh tokens** - request a new access token when expired.
- The `.http` file automatically extracts and uses the latest token.

## Notes

- Pre-signed S3 URLs are valid for 12 hours.
- All endpoints require authentication (no anonymous access).
- JSON responses use `snake_case` naming convention.
