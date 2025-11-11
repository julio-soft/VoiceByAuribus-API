# VoiceByAuribus API - AI Agent Instructions

## Architecture: Vertical Slice + Clean Architecture Hybrid

This project follows **Vertical Slice Architecture** with Clean Architecture principles:

```
Features/                    # Feature-based vertical slices
  Auth/                      # Authentication feature (all layers)
    Application/{Dtos,Services}/
    Presentation/{Controllers,Security}/
    AuthModule.cs            # Feature DI registration
  Voices/                    # Voice models feature (all layers)
    Domain/VoiceModel.cs
    Application/{Dtos,Mappings,Services}/
    Infrastructure/Data/     # Feature-specific EF configurations
    Presentation/Controllers/
    VoicesModule.cs          # Feature DI registration
Shared/                      # Cross-cutting concerns only
  Domain/                    # BaseAuditableEntity, ISoftDelete, IHasUserId
  Interfaces/                # ICurrentUserService, IDateTimeProvider, IS3PresignedUrlService
  Infrastructure/{Data,Services,Filters}/
```

**Critical Pattern**: Each feature is self-contained with its own layers. Add new features by creating `Features/NewFeature/` with `NewFeatureModule.cs` for DI registration, then call `builder.Services.AddNewFeature()` in `Program.cs`.

## Database & EF Core

- **DbContext**: `Shared/Infrastructure/Data/ApplicationDbContext.cs` - central context referencing all feature entities
- **Entity Configurations**: Place in `Features/{Feature}/Infrastructure/Data/{Entity}Configuration.cs`
- **Global Filters**: Automatic soft-delete and user ownership filters applied via `ModelBuilderExtensions.ApplyGlobalFilters()`
  - `ISoftDelete`: Entities with `IsDeleted` auto-filtered from queries
  - `IHasUserId`: Entities scoped to current user (unless user is admin)
- **Auditing**: `CreatedAt`/`UpdatedAt` set automatically in `SaveChangesAsync()` via `BaseAuditableEntity`
- **Migrations**: Run from project root: `dotnet ef migrations add MigrationName`, `dotnet ef database update`

## Authentication & Authorization (AWS Cognito M2M)

**Key Insight**: Cognito M2M tokens don't include standard `aud` claim. See `.ai_doc/COGNITO_M2M_AUTH.md` for details.

- **Token Validation**: `ValidateAudience=false` in `Program.cs`, custom scope validation in `OnTokenValidated` event
- **Scopes**: `voice-by-auribus-api/base` (read), `voice-by-auribus-api/admin` (internal paths exposed)
- **Policies**: Defined in `Features/Auth/Presentation/AuthorizationPolicies.cs` and `AuthorizationScopes.cs`
- **Current User**: `ICurrentUserService` extracts claims from JWT (sub → UserId, scope → Scopes, etc.)

## S3 Pre-Signed URLs

- **Pattern**: Store `s3://bucket/key` URIs in database, generate 12-hour pre-signed URLs at runtime via `IS3PresignedUrlService`
- **Admin-Only Paths**: `VoiceModel.VoiceModelPath` and `VoiceModelIndexPath` only returned when `ICurrentUserService.IsAdmin == true`
- **Example**: See `Features/Voices/Application/Services/VoiceModelService.cs` → `MapVoiceModel()` method

## Development Workflows

```bash
# Start dependencies
docker-compose up -d postgres

# Build & run
dotnet build
dotnet run  # Runs on http://localhost:5037

# Kill port conflicts
lsof -ti:5037 | xargs kill -9

# Testing
# Use api-tests.http with REST Client extension
# Acquire token first via /token endpoint, then test other endpoints
```

## Conventions

- **JSON**: `snake_case_lower` via `JsonNamingPolicy.SnakeCaseLower` in `Program.cs`
- **API Versioning**: URL-based (e.g., `/api/v1/auth/status`), configured with `Asp.Versioning`
- **Validation**: FluentValidation with `ValidationFilter` (returns 400 with `ValidationProblemDetails`)
- **Namespaces**: Match folder structure: `VoiceByAuribus_API.Features.{Feature}.{Layer}`
- **Documentation**: Endpoint docs in `.ai_doc/v1/`, no markdown outside `.github/` or `.ai_doc/`
- **API Response**: All endpoints use `ApiResponse<T>` wrapper with `success`, `message`, `data`, and `errors` fields
- **Controllers**: Inherit from `BaseController` in `Shared/Infrastructure/Controllers/` for standardized responses and current user access

## Adding a New Feature

1. Create `Features/NewFeature/{Domain,Application,Infrastructure,Presentation}/`
2. Create `Features/NewFeature/NewFeatureModule.cs` with `AddNewFeature()` extension method
3. Register services in the module (e.g., `services.AddScoped<INewService, NewService>()`)
4. Call `builder.Services.AddNewFeature()` in `Program.cs`
5. Add entity configurations in `Features/NewFeature/Infrastructure/Data/`
6. DbContext will auto-discover configurations via `ApplyConfigurationsFromAssembly()`

## Key Files to Reference

- **Feature DI**: `Features/Auth/AuthModule.cs`, `Features/Voices/VoicesModule.cs`
- **Global Filters**: `Shared/Infrastructure/Data/ModelBuilderExtensions.cs`
- **Auth Setup**: `Program.cs` → `ConfigureAuthentication()` and `ConfigureAuthorization()`
- **Shared Services**: `Shared/Infrastructure/Services/{CurrentUserService,S3PresignedUrlService}.cs`
- **Base Controller**: `Shared/Infrastructure/Controllers/BaseController.cs` - provides `Success<T>()`, `Error<T>()`, and `GetUserId()`
- **API Response**: `Shared/Domain/ApiResponse.cs` - standardized response wrapper for all endpoints
