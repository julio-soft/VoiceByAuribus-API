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
    Presentation/Controllers/
    VoicesModule.cs          # Feature DI registration
  AudioFiles/                # Audio files upload and preprocessing feature
    Domain/{AudioFile,AudioPreprocessing,UploadStatus,ProcessingStatus}.cs
    Application/{Dtos,Services,Validators}/
    Presentation/Controllers/
    AudioFilesModule.cs      # Feature DI registration
Shared/                      # Cross-cutting concerns only
  Domain/                    # BaseAuditableEntity, ISoftDelete, IHasUserId, ApiResponse
  Interfaces/                # ICurrentUserService, IDateTimeProvider, IS3PresignedUrlService, ISqsService
  Infrastructure/
    Data/
      ApplicationDbContext.cs
      ModelBuilderExtensions.cs
      Configurations/        # ALL EF Core entity configurations (organized by feature)
    Services/                # CurrentUserService, S3PresignedUrlService, SqsService, etc.
    Filters/                 # ValidationFilter, WebhookAuthenticationAttribute
    Middleware/              # GlobalExceptionHandlerMiddleware
    Controllers/             # BaseController
VoiceByAuribus.AudioUploadNotifier/  # AWS Lambda for S3 upload notifications
```

**Critical Pattern**: Each feature is self-contained with its own layers. Add new features by creating `Features/NewFeature/` with `NewFeatureModule.cs` for DI registration, then call `builder.Services.AddNewFeature()` in `Program.cs`.

## Database & EF Core

- **DbContext**: `Shared/Infrastructure/Data/ApplicationDbContext.cs` - central context referencing all feature entities
- **Entity Configurations**: All `IEntityTypeConfiguration<T>` implementations must be placed in `Shared/Infrastructure/Data/Configurations/`
  - Name pattern: `{EntityName}Configuration.cs` (e.g., `VoiceModelConfiguration.cs`)
  - Add XML comment indicating the feature: `/// <summary>Entity Framework configuration for {Entity} ({Feature} feature)</summary>`
  - Configurations are auto-discovered via `ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly)`
- **Global Filters**: Automatic soft-delete and user ownership filters applied via `ModelBuilderExtensions.ApplyGlobalFilters()`
  - `ISoftDelete`: Entities with `IsDeleted` auto-filtered from queries
  - `IHasUserId`: Entities scoped to current user (unless user is admin)
- **Auditing**: `CreatedAt`/`UpdatedAt` set automatically in `SaveChangesAsync()` via `BaseAuditableEntity`
- **Migrations**: Run from solution root: `dotnet ef migrations add MigrationName --project VoiceByAuribus.API/VoiceByAuribus-API.csproj`, `dotnet ef database update --project VoiceByAuribus.API/VoiceByAuribus-API.csproj`

## Authentication & Authorization (AWS Cognito M2M)

**Key Insight**: Cognito M2M tokens don't include standard `aud` claim. See `.ai_doc/COGNITO_M2M_AUTH.md` for details.

- **Token Validation**: `ValidateAudience=false` in `Program.cs`, custom scope validation in `OnTokenValidated` event
- **Scopes**: `voice-by-auribus-api/base` (read), `voice-by-auribus-api/admin` (internal paths exposed)
- **Policies**: Defined in `Features/Auth/Presentation/AuthorizationPolicies.cs` and `AuthorizationScopes.cs`
- **Current User**: `ICurrentUserService` extracts claims from JWT (sub → UserId, scope → Scopes, etc.)

## S3 Pre-Signed URLs

- **Pattern**: Store `s3://bucket/key` URIs in database, generate pre-signed URLs at runtime via `IS3PresignedUrlService`
- **GET URLs**: 12-hour lifetime for downloading (voice models, processed audio)
- **PUT URLs**: 30-minute lifetime for uploading (audio files), with size constraints
- **Admin-Only Paths**: `VoiceModel.VoiceModelPath` and `VoiceModelIndexPath` only returned when `ICurrentUserService.IsAdmin == true`
- **Example**: See `Features/Voices/Application/Services/VoiceModelService.cs` → `MapVoiceModel()` method

## Audio Files & Preprocessing

**Feature**: Audio file upload with automatic preprocessing pipeline

- **Upload Flow**: Client creates record → receives pre-signed PUT URL → uploads to S3 → Lambda notifies backend → preprocessing triggered
- **S3 Structure**: `audio-files/{userId}/{temp|short|inference}/{fileId}.ext`
- **Processing**: External service reads from SQS, generates 10s preview + inference-ready file, callbacks backend
- **User Ownership**: AudioFile implements `IHasUserId` for automatic user isolation
- **Admin Data**: S3 URIs and preprocessing details only visible to admins
- **Webhooks**: `/webhook/upload-notification` and `/webhook/preprocessing-result` use `WebhookAuthenticationAttribute` with API key
- **Documentation**: See `.ai_doc/v1/audio_files.md` and `.ai_doc/AWS_RESOURCES.md`

## Development Workflows

```bash
# Start dependencies
cd VoiceByAuribus.API
docker-compose up -d postgres

# Build & run (from solution root)
cd ..
dotnet build
dotnet run --project VoiceByAuribus.API/VoiceByAuribus-API.csproj  # Runs on http://localhost:5037

# Or from project directory
cd VoiceByAuribus.API
dotnet run  # Runs on http://localhost:5037

# Kill port conflicts
lsof -ti:5037 | xargs kill -9

# EF Core migrations (run from solution root)
dotnet ef migrations add MigrationName --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
dotnet ef database update --project VoiceByAuribus.API/VoiceByAuribus-API.csproj

# Testing
# API: Use VoiceByAuribus.API/api-tests.http with REST Client extension
# Lambda: Run tests with dotnet test VoiceByAuribus.AudioUploadNotifier/test/VoiceByAuribus.AudioUploadNotifier.Tests/VoiceByAuribus.AudioUploadNotifier.Tests.csproj

# Build/Test entire solution
dotnet build VoiceByAuribus-API.sln
dotnet test VoiceByAuribus-API.sln
```

## Conventions

- **JSON**: `snake_case_lower` via `JsonNamingPolicy.SnakeCaseLower` in `Program.cs`
- **API Versioning**: URL-based (e.g., `/api/v1/auth/status`), configured with `Asp.Versioning`
- **Validation**: FluentValidation with `ValidationFilter` (returns 400 with `ValidationProblemDetails`)
- **Namespaces**: Match folder structure: `VoiceByAuribus_API.Features.{Feature}.{Layer}`
- **Documentation**: Endpoint docs in `.ai_doc/v1/`, no markdown outside `.github/` or `.ai_doc/`
- **API Response**: All endpoints use `ApiResponse<T>` wrapper with `success`, `message`, `data`, and `errors` fields
- **Controllers**: Inherit from `BaseController` in `Shared/Infrastructure/Controllers/` for standardized responses and current user access

## Solution Structure

The solution includes multiple projects organized hierarchically:

```
VoiceByAuribus-API.sln                    # Solution file at root
├── VoiceByAuribus.API/                   # Main API project
│   └── VoiceByAuribus-API.csproj
└── VoiceByAuribus.AudioUploadNotifier/   # Lambda function project group
    ├── src/
    │   └── VoiceByAuribus.AudioUploadNotifier/
    │       └── VoiceByAuribus.AudioUploadNotifier.csproj
    └── test/
        └── VoiceByAuribus.AudioUploadNotifier.Tests/
            └── VoiceByAuribus.AudioUploadNotifier.Tests.csproj
```

To build all projects: `dotnet build VoiceByAuribus-API.sln`
To test all projects: `dotnet test VoiceByAuribus-API.sln`

## Adding a New Feature

1. Create `Features/NewFeature/{Domain,Application,Presentation}/` (no Infrastructure folder needed)
2. Create entity in `Features/NewFeature/Domain/NewEntity.cs`
3. Create entity configuration in `Shared/Infrastructure/Data/Configurations/NewEntityConfiguration.cs` with XML comment indicating feature
4. Create `Features/NewFeature/NewFeatureModule.cs` with `AddNewFeature()` extension method
5. Register services in the module (e.g., `services.AddScoped<INewService, NewService>()`)
6. Call `builder.Services.AddNewFeature()` in `Program.cs`
7. DbContext will auto-discover configurations via `ApplyConfigurationsFromAssembly()`

## AWS Services Integration

- **S3**: Audio file storage, voice model storage
- **SQS**: Audio preprocessing queue (`ISqsService` for sending messages)
- **Lambda**: S3 upload event handler (`VoiceByAuribus.AudioUploadNotifier`)
- **Cognito**: M2M authentication with resource server scopes
- **Configuration**: AWS resources configured in `appsettings.json` under `AWS:S3` and `AWS:SQS`

## Key Files to Reference

- **Feature DI**: `Features/Auth/AuthModule.cs`, `Features/Voices/VoicesModule.cs`, `Features/AudioFiles/AudioFilesModule.cs`
- **Global Filters**: `Shared/Infrastructure/Data/ModelBuilderExtensions.cs`
- **Auth Setup**: `Program.cs` → `ConfigureAuthentication()` and `ConfigureAuthorization()`
- **Shared Services**: `Shared/Infrastructure/Services/{CurrentUserService,S3PresignedUrlService,SqsService}.cs`
- **Base Controller**: `Shared/Infrastructure/Controllers/BaseController.cs` - provides `Success<T>()`, `Error<T>()`, and `GetUserId()`
- **API Response**: `Shared/Domain/ApiResponse.cs` - standardized response wrapper for all endpoints
- **Webhook Auth**: `Shared/Infrastructure/Filters/WebhookAuthenticationAttribute.cs` - validates `X-Webhook-Api-Key` header
- **Lambda Function**: `VoiceByAuribus.AudioUploadNotifier/src/VoiceByAuribus.AudioUploadNotifier/Function.cs`
