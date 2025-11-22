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
- **Webhooks**: `/webhooks/upload-notification` and `/webhooks/preprocessing-result` use `WebhookAuthenticationAttribute` with API key
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

### Mappers: Static Classes Pattern
Mappers MUST be static classes in separate files at `Features/{Feature}/Application/Mappers/`:
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
**Rules**: Static methods, XML documentation, admin data via `isAdmin` parameter, call other mappers for nested entities, no business logic

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
