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
  VoiceConversions/          # Voice conversion feature
    Domain/{VoiceConversion,ConversionStatus,Transposition}.cs
    Application/{Dtos,Services,Validators,Helpers,BackgroundServices}/
    Presentation/{Controllers,Webhooks}/
    VoiceConversionsModule.cs  # Feature DI registration
  WebhookSubscriptions/      # Webhook notifications feature
    Domain/{WebhookSubscription,WebhookDeliveryLog,WebhookEvent,WebhookDeliveryStatus}.cs
    Application/{Dtos,Services,Validators,BackgroundServices}/
    Presentation/Controllers/
    WebhookSubscriptionsModule.cs  # Feature DI registration
Shared/                      # Cross-cutting concerns only
  Domain/                    # BaseAuditableEntity, ISoftDelete, IHasUserId, ApiResponse
  Interfaces/                # ICurrentUserService, IDateTimeProvider, IS3PresignedUrlService, ISqsService, IHealthCheckService
  Infrastructure/
    Data/
      ApplicationDbContext.cs
      ModelBuilderExtensions.cs
      Configurations/        # ALL EF Core entity configurations (organized by feature)
    Services/                # CurrentUserService, S3PresignedUrlService, SqsService, SqsQueueResolver, HealthCheckService, etc.
    Configuration/           # AwsSecretsManagerConfigurationProvider
    Filters/                 # ValidationFilter, WebhookAuthenticationAttribute
    Middleware/              # GlobalExceptionHandlerMiddleware
    Controllers/             # BaseController
VoiceByAuribus.AudioUploadNotifier/  # AWS Lambda for S3 upload notifications
```

**Critical Pattern**: Each feature is self-contained with its own layers. Add new features by creating `Features/NewFeature/` with `NewFeatureModule.cs` for DI registration, then call `builder.Services.AddNewFeature()` in `Program.cs`.

## Database & EF Core

- **DbContext**: `Shared/Infrastructure/Data/ApplicationDbContext.cs` - central context referencing all feature entities
- **Tables**: `voice_models`, `audio_files`, `audio_preprocessings`, `voice_conversions`, `webhook_subscriptions`, `webhook_delivery_logs`
- **Entity Configurations**: All `IEntityTypeConfiguration<T>` implementations must be placed in `Shared/Infrastructure/Data/Configurations/`
  - Name pattern: `{EntityName}Configuration.cs` (e.g., `VoiceModelConfiguration.cs`, `VoiceConversionConfiguration.cs`)
  - Add XML comment indicating the feature: `/// <summary>Entity Framework configuration for {Entity} ({Feature} feature)</summary>`
  - Configurations are auto-discovered via `ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly)`
- **Global Filters**: Automatic soft-delete and user ownership filters applied via `ModelBuilderExtensions.ApplyGlobalFilters()`
  - `ISoftDelete`: Entities with `IsDeleted` auto-filtered from queries
  - `IHasUserId`: Entities scoped to current user (unless user is admin)
- **Auditing**: `CreatedAt`/`UpdatedAt` set automatically in `SaveChangesAsync()` via `BaseAuditableEntity`
- **Optimistic Concurrency**: `VoiceConversion` uses `RowVersion` for safe multi-instance updates
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

## Features Overview

### Audio Files & Preprocessing

- **Upload Flow**: Client creates record → receives pre-signed PUT URL → uploads to S3 → Lambda notifies backend → preprocessing triggered
- **S3 Structure**: `audio-files/{userId}/{temp|short|inference}/{fileId}.ext`
- **Processing**: External service reads from SQS (`aurivoice-svs-prep-nbl.fifo`), generates 10s preview + inference-ready file, callbacks backend
- **User Ownership**: AudioFile implements `IHasUserId` for automatic user isolation
- **Admin Data**: S3 URIs and preprocessing details only visible to admins
- **Webhooks**: `/webhooks/upload-notification` and `/webhooks/preprocessing-result` use `WebhookAuthenticationAttribute` with API key

### Voice Conversions

- **Background Processing**: `VoiceConversionProcessorService` polls every 3 seconds for pending conversions
- **Status Flow**: `PendingPreprocessing` → `Queued` → `Processing` → `Completed`/`Failed`
- **Optimistic Locking**: Uses `RowVersion` to prevent race conditions in multi-instance deployments
- **Retry Logic**: Max 5 attempts with 5-minute delay between retries
- **Queues**: Sends to `VoiceInferenceQueue` (full audio) or `PreviewInferenceQueue` (10s preview)
- **Pitch Shifting**: ⭐ **CRITICAL CONVENTION** - ALWAYS use `pitch_shift` abstraction in external APIs
  - **Internal**: `Transposition` enum (`SameOctave`, `LowerOctave`, etc.)
  - **External API**: `pitch_shift` string (`"same_octave"`, `"lower_octave"`, etc.)
  - **Helper**: `PitchShiftHelper.ToTransposition()` and `PitchShiftHelper.ToPitchShiftString()`
  - **Valid values**: `same_octave`, `lower_octave`, `higher_octave`, `third_down`, `third_up`, `fifth_down`, `fifth_up`
  - **Rule**: NEVER expose `Transposition` enum values in API responses or webhook payloads
- **Health Monitoring**: Integrated with `IHealthCheckService` for processor status tracking
- **User Ownership**: Implements `IHasUserId` for automatic user isolation
- **Configuration**: Background processor settings in `appsettings.json` under `VoiceConversions:BackgroundProcessor`
- **Webhook Events**: Publishes webhook events on completion/failure via `IWebhookEventPublisher`

### Webhook Subscriptions ⭐ NEW FEATURE

- **Purpose**: Subscription-based webhook notifications for external clients when voice conversions complete/fail
- **Security**: HTTPS-only with HMAC-SHA256 signature verification (format: `sha256={hex}`)
- **Secret Management**: ⭐ **Auto-Generated Secrets** with AES-256-GCM encryption
  - Secrets are **automatically generated** (64-char hex) when creating subscriptions - NOT user-provided
  - Encrypted with AES-256-GCM using master key from AWS Secrets Manager
  - Plain text secret shown ONLY once in `CreatedWebhookSubscriptionResponseDto` after creation/regeneration
  - Can be regenerated via `/regenerate-secret` endpoint if lost
- **Event-Agnostic Design**: ⭐ **CRITICAL ARCHITECTURE** - Webhook system supports ANY future event type
  - `WebhookDeliveryLog` uses generic fields: `EventType` (string), `EntityType` (string), `EntityId` (Guid?)
  - **Type Safety**: ALWAYS use constants from `Shared/Domain/WebhookEventTypes.cs` and `WebhookEntityTypes.cs`
    - Current: `WebhookEventTypes.ConversionCompleted`, `WebhookEventTypes.ConversionFailed`, `WebhookEventTypes.Test`
    - Current: `WebhookEntityTypes.VoiceConversion`, `WebhookEntityTypes.Test`
  - NO foreign key constraints to specific entities (e.g., voice_conversions)
  - Supports current events: `conversion.completed`, `conversion.failed`, `webhook.test`
  - Easily extensible to future events: `training.completed`, `training.failed`, etc.
  - Keep backwards-compatible `Event` enum for filtering, but always use `EventType` string constants for new features
- **Payload Conventions**: ⭐ **CRITICAL** - NEVER include temporary URLs in webhook payloads
  - Payloads use `pitch_shift` abstraction (NOT `transposition` enum) - see Voice Conversions section
  - **DO NOT include `output_url`** - pre-signed URLs expire in 12 hours and shouldn't be stored
  - Clients must call `GET /api/v1/voice-conversions/{id}` to get fresh URLs (valid 12h)
  - See `.ai_doc/WEBHOOK_AND_API_CONVENTIONS.md` for complete payload design rationale
- **Optimistic Locking**: ⭐ **CRITICAL for Multi-Instance Deployments**
  - `WebhookDeliveryLog` uses `RowVersion` (bytea) for optimistic concurrency control
  - Triple-barrier security: (1) Query Filter excludes Processing, (2) Status Double-Check with stuck detection, (3) Optimistic Lock on SaveChanges
  - Processing status set BEFORE HTTP call to prevent duplicate deliveries
  - Stuck webhook recovery: Processing webhooks older than 5 minutes are retried (handles crashed instances)
  - Each webhook processed in separate DbContext scope for proper change tracking
  - DbUpdateConcurrencyException thrown if another instance modifies same webhook
- **Background Processing**: `WebhookDeliveryProcessorService` polls every 5 seconds for failed/pending deliveries
  - Batch processing with configurable batch size (default: 20)
  - Processes deliveries individually with Optimistic Locking
  - Automatic recovery of stuck webhooks (Processing > 5 minutes)
  - Configuration in `appsettings.json`: IntervalSeconds, BatchSize, ProcessingTimeoutMinutes
- **Test Webhook**: ⭐ **Fire-and-Forget Pattern**
  - Test webhooks sent asynchronously via Task.Run() without blocking request
  - NOT saved to database to prevent cluttering delivery logs
  - Test failures do NOT count toward auto-disable threshold
  - Client validates by monitoring their own webhook endpoint
  - Returns immediate response with test payload for reference
- **Retry Logic**: Max 5 attempts with exponential backoff (2^attempt seconds: 2s, 4s, 8s, 16s, 32s)
- **Auto-Disable**: Subscriptions auto-disabled after 10 consecutive failures (configurable via `MaxConsecutiveFailures`)
- **SSRF Protection**: Blocks localhost, 127.0.0.1, and private IP ranges (10.x, 192.168.x, 172.16-31.x)
- **Endpoints**: Complete CRUD + regenerate-secret + test + delivery logs (8 endpoints total)
- **Limits**: Max 5 active subscriptions per user
- **Headers**: `X-Webhook-Signature`, `X-Webhook-Timestamp`, `X-Webhook-Id`, `X-Webhook-Event`
- **HTTP Client**: IHttpClientFactory with 30-second timeout, TLS 1.2/1.3 only
- **Configuration**: Settings in `appsettings.json` under `Webhooks:BackgroundProcessor` and `Webhooks:Client`
- **Documentation**: Complete API docs in `.ai_doc/v1/webhooks.md` with Node.js/Python verification examples
- **Mappers**: Extension methods in `Features/WebhookSubscriptions/Application/Mappers/WebhookSubscriptionMappers.cs`

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

## Logging

- **Framework**: Serilog (replaces default .NET logging)
- **Format**: Structured JSON output via `CompactJsonFormatter`
- **Enrichment**: RequestId, MachineName, ThreadId, EnvironmentName, RemoteIP, UserId, UserAgent
- **Request Logging**: HTTP method, path, status code, elapsed time
- **Levels**: Information (default), Warning (4xx, ASP.NET Core, EF Core), Error (5xx, exceptions)
- **Configuration**: In `appsettings.json` under `Serilog` section

## Conventions

- **JSON**: `snake_case_lower` via `JsonNamingPolicy.SnakeCaseLower` in `Program.cs`
- **API Versioning**: URL-based (e.g., `/api/v1/auth/status`), configured with `Asp.Versioning`
- **Validation**: 
  - **Data Annotations**: Use for simple validations (required, length, regex)
  - **FluentValidation**: Use ONLY for complex validations (cross-property, async, conditional, dependency injection)
  - `ValidationFilter` returns 400 with `ValidationProblemDetails`
- **Namespaces**: Match folder structure: `VoiceByAuribus_API.Features.{Feature}.{Layer}`
- **Documentation**: Endpoint docs in `.ai_doc/v1/`, no markdown outside `.github/` or `.ai_doc/`
- **API Response**: All endpoints use `ApiResponse<T>` wrapper with `success`, `message`, `data`, and `errors` fields
- **Controllers**: Inherit from `BaseController` in `Shared/Infrastructure/Controllers/` for standardized responses and current user access
- **Async Patterns**: All I/O operations must be async, methods end with `Async`, return `Task<T>` or `Task`
- **Naming Conventions**:
  - **C# Code**: PascalCase for classes/methods/properties, _camelCase for private fields, camelCase for parameters
  - **Database Tables**: snake_case plural (e.g., `audio_files`, `voice_models`)
  - **API Routes**: kebab-case (e.g., `/api/v1/audio-files`)
  - **Enums**: Convert to strings in DTOs and database (via `.HasConversion<string>()`)
- **XML Documentation**: Required for all public classes, methods, and properties

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
- **SQS**: Multiple queues for async processing
  - `aurivoice-svs-prep-nbl.fifo`: Audio preprocessing (FIFO)
  - `VoiceInferenceQueue`: Full audio voice conversions
  - `PreviewInferenceQueue`: Preview (10s) voice conversions
  - Use `SqsQueueResolver` for queue name → URL resolution with caching
- **Lambda**: S3 upload event handler (`VoiceByAuribus.AudioUploadNotifier`)
- **Cognito**: M2M authentication with resource server scopes
- **Secrets Manager**: Production configuration via `AwsSecretsManagerConfigurationProvider` with retry logic
- **Configuration**: Queue names (not URLs) in `appsettings.json` under `AWS:SQS`, resolved at runtime

## Key Files to Reference

- **Feature DI**: `Features/Auth/AuthModule.cs`, `Features/Voices/VoicesModule.cs`, `Features/AudioFiles/AudioFilesModule.cs`, `Features/VoiceConversions/VoiceConversionsModule.cs`
- **Background Services**: `Features/VoiceConversions/Application/BackgroundServices/VoiceConversionProcessorService.cs`
- **Global Filters**: `Shared/Infrastructure/Data/ModelBuilderExtensions.cs`
- **Auth Setup**: `Program.cs` → `ConfigureAuthentication()` and `ConfigureAuthorization()`
- **Shared Services**: `Shared/Infrastructure/Services/{CurrentUserService,S3PresignedUrlService,SqsService,SqsQueueResolver,HealthCheckService}.cs`
- **Configuration**: `Shared/Infrastructure/Configuration/AwsSecretsManagerConfigurationProvider.cs`
- **Base Controller**: `Shared/Infrastructure/Controllers/BaseController.cs` - provides `Success<T>()`, `Error<T>()`, and `GetUserId()`
- **API Response**: `Shared/Domain/ApiResponse.cs` - standardized response wrapper for all endpoints
- **Webhook Auth**: `Shared/Infrastructure/Filters/WebhookAuthenticationAttribute.cs` - validates `X-Webhook-Api-Key` header
- **Lambda Function**: `VoiceByAuribus.AudioUploadNotifier/src/VoiceByAuribus.AudioUploadNotifier/Function.cs`

## Coding Patterns

### Services: Primary Constructor Pattern
Services use primary constructor with dependency injection:
```csharp
public class AudioFileService(
    ApplicationDbContext context,
    IS3PresignedUrlService presignedUrlService,
    IDateTimeProvider dateTimeProvider) : IAudioFileService
{
    // Constructor parameters auto-assigned as fields
    // No need for manual field declarations
    
    // Extract configuration in constructor
    private readonly string _audioBucket = configuration["AWS:S3:AudioFilesBucket"]
        ?? throw new InvalidOperationException("AWS:S3:AudioFilesBucket configuration is required");
}
```

### Mappers: Two Patterns

Mappers MUST be in separate files at `Features/{Feature}/Application/Mappers/`. Two patterns are acceptable:

**Pattern 1: Static Class Methods** (used for most features)
```csharp
/// <summary>Mapper for AudioFile entity to DTOs.</summary>
public static class AudioFileMapper
{
    /// <summary>Maps AudioFile entity to AudioFileResponseDto.</summary>
    /// <param name="audioFile">The audio file entity to map</param>
    /// <param name="isAdmin">Whether the current user is an admin</param>
    public static AudioFileResponseDto MapToResponseDto(AudioFile audioFile, bool isAdmin)
    {
        var dto = new AudioFileResponseDto { /* mapping */ };

        // Admin-specific data conditionally included
        if (isAdmin)
        {
            dto.S3Uri = audioFile.S3Uri;
            dto.Preprocessing = AudioPreprocessingMapper.MapToResponseDto(audioFile.Preprocessing);
        }

        return dto;
    }
}
```

**Pattern 2: Extension Methods** (used for webhooks)
```csharp
/// <summary>Extension methods for mapping WebhookSubscription entities to DTOs.</summary>
public static class WebhookSubscriptionMappers
{
    /// <summary>Maps WebhookSubscription entity to response DTO.</summary>
    public static WebhookSubscriptionResponseDto ToResponseDto(this WebhookSubscription subscription)
    {
        return new WebhookSubscriptionResponseDto
        {
            Id = subscription.Id,
            Url = subscription.Url,
            // ... other fields
        };
    }
}

// Usage: subscription.ToResponseDto()
```

**Rules**: Static methods, XML documentation, admin data via `isAdmin` parameter, call other mappers for nested entities, no business logic. Use extension methods when fluent API style improves readability.

### Entities: Base Classes and Interfaces
```csharp
/// <summary>Represents an audio file uploaded by a user.</summary>
public class AudioFile : BaseAuditableEntity, IHasUserId, ISoftDelete
{
    public Guid? UserId { get; set; }  // IHasUserId: automatic user-scoped filtering
    public required string FileName { get; set; }
    public bool IsDeleted { get; set; }  // ISoftDelete: automatic soft-delete filtering
    // BaseAuditableEntity provides: Id, CreatedAt, UpdatedAt (automatic timestamps)
}
```

### EF Core Configurations
Entity configurations MUST be in `Shared/Infrastructure/Data/Configurations/`:
```csharp
/// <summary>Entity Framework configuration for AudioFile (AudioFiles feature).</summary>
public class AudioFileConfiguration : IEntityTypeConfiguration<AudioFile>
{
    public void Configure(EntityTypeBuilder<AudioFile> builder)
    {
        builder.ToTable("audio_files");  // snake_case table name
        
        builder.Property(x => x.FileName)
            .IsRequired()
            .HasMaxLength(255);
        
        builder.Property(x => x.UploadStatus)
            .IsRequired()
            .HasConversion<string>();  // Convert enums to strings
        
        // Add indexes for FK and frequently queried fields
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.S3Uri).IsUnique();
    }
}
```
**Rules**: Snake_case table names, convert enums to strings, add indexes for FKs and query fields, specify max lengths

### Controllers: Standardized Responses
```csharp
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/audio-files")]
public class AudioFilesController(
    IAudioFileService audioFileService,
    ICurrentUserService currentUserService) : BaseController
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AudioFileResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAudioFile([FromRoute] Guid id)
    {
        var userId = currentUserService.GetUserId();
        var isAdmin = currentUserService.IsAdmin;
        var audioFile = await audioFileService.GetAudioFileByIdAsync(id, userId, isAdmin);
        
        if (audioFile is null)
            return NotFound<AudioFileResponseDto>("Audio file not found");
        
        return Success(audioFile);
    }
}
```

## Security & Error Handling

### User Ownership Filtering
Always filter by `UserId` for user-owned resources (global filter applies automatically via `IHasUserId`):
```csharp
// Global filter ensures user isolation, but explicit checks recommended
var audioFile = await context.AudioFiles
    .FirstOrDefaultAsync(af => af.Id == id && af.UserId == userId);
```

### Admin-Only Data
Use `isAdmin` parameter to conditionally expose sensitive data (S3 URIs, preprocessing details):
```csharp
if (isAdmin)
{
    dto.S3Uri = audioFile.S3Uri;
    dto.Preprocessing = /* preprocessing details */;
}
```

### Error Handling Pattern
- Services return `null` for not found items
- Services throw exceptions for invalid operations
- Controllers convert `null` to 404 responses
- Global exception handler (`GlobalExceptionHandlerMiddleware`) catches unhandled exceptions

```csharp
// Service layer
public async Task<AudioFileResponseDto?> GetByIdAsync(Guid id, Guid userId)
{
    var audioFile = await context.AudioFiles
        .AsNoTracking()  // Use for read-only queries
        .FirstOrDefaultAsync(af => af.Id == id && af.UserId == userId);
    
    return audioFile is null ? null : AudioFileMapper.MapToResponseDto(audioFile, false);
}

// Controller layer
[HttpGet("{id:guid}")]
public async Task<IActionResult> GetAudioFile([FromRoute] Guid id)
{
    var audioFile = await audioFileService.GetByIdAsync(id, userId);
    if (audioFile is null)
        return NotFound<AudioFileResponseDto>("Audio file not found");
    
    return Success(audioFile);
}
```

## Testing Patterns

### Unit Tests
```csharp
[Fact]
public async Task CreateAudioFileAsync_WithValidData_ReturnsCreatedResponse()
{
    // Arrange
    var dto = new CreateAudioFileDto { FileName = "test.mp3", MimeType = "audio/mpeg" };
    var userId = Guid.NewGuid();
    
    // Act
    var result = await _audioFileService.CreateAudioFileAsync(dto, userId);
    
    // Assert
    Assert.NotNull(result);
    Assert.Equal(dto.FileName, result.FileName);
}
```

### Integration Tests
Use `WebApplicationFactory<Program>` for end-to-end endpoint testing with in-memory or test database
