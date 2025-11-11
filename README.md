# VoiceByAuribus API

ASP.NET Core 10 Web API implementing clean architecture with vertical slices, AWS Cognito authentication, and PostgreSQL persistence.

## Prerequisites

- .NET SDK 10.0 (RC) or later
- PostgreSQL 14+
- AWS account with Cognito User Pool `us-east-1_2GQIgX9Vw` and S3 bucket access for voice assets
- AWS credentials configured locally (shared profile or environment variables)

## Configuration

1. **PostgreSQL**: Connection string is pre-configured for Docker Compose setup (see `docker-compose.yml`).
2. **AWS**: Ensure the `AWS` section matches your credential profile and region.
3. **Cognito Client Secret**: Update `api-tests.http` with your client secret for testing.
4. **S3 URIs**: Provide valid S3 URIs (`s3://bucket/key`) for voice models in the database. The API returns 12-hour pre-signed links.

## Database

- Apply EF Core migrations once they are created: `dotnet ef database update`.
- Entities automatically track `created_at`, `updated_at`, and soft-delete columns.

## Authentication

- JWT Bearer auth via AWS Cognito.
- Required scopes:
  - `voice-by-auribus-api/base` for basic access.
  - `voice-by-auribus-api/admin` for administrative operations that expose internal model metadata.

## Running the API

```bash
# Start PostgreSQL with Docker Compose
docker-compose up -d postgres

# Apply database migrations (first time only)
dotnet ef migrations add InitialCreate
dotnet ef database update

# Run the development server
dotnet run
```

The default build task is available as **dotnet build** in VS Code.

## API Documentation

- **HTTP Client Tests**: `api-tests.http` (requires VS Code REST Client extension or similar)
- **Testing Guide**: `.ai_doc/API_TESTING.md`
- Runtime OpenAPI description: `/openapi/v1.json` (development only)
- Endpoint documentation: `.ai_doc/v1/`

### API Response Format

All endpoints return a standardized response format:

```json
{
  "success": true,
  "message": "Optional success message",
  "data": { /* Response data */ },
  "errors": null
}
```

Error responses:

```json
{
  "success": false,
  "message": "Error description",
  "data": null,
  "errors": ["Detailed error 1", "Detailed error 2"]
}
```

## Project Structure

- `Features/{FeatureName}/`: Vertical slices containing Domain, Application, and Presentation layers per feature
- `Shared/`: Cross-cutting concerns including base entities, interfaces, and infrastructure services
- `Shared/Infrastructure/Data/Configurations/`: All EF Core entity configurations (centralized)
- `.ai_doc/`: human-authored endpoint narratives.

## Next Steps

- Create and apply EF Core migrations for the voice model schema.
- Implement write operations and admin tooling as requirements evolve.
