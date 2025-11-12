# Architecture Documentation

## Overview

VoiceByAuribus API follows **Vertical Slice Architecture** with Clean Architecture principles. This hybrid approach organizes code by features rather than technical layers, while maintaining clean separation of concerns within each feature.

## Core Architectural Principle

> Each feature is self-contained with its own Domain, Application, and Presentation layers. Infrastructure concerns are shared across features to avoid duplication.

## Project Structure

```
VoiceByAuribus.API/
├── Features/                           # Feature-based vertical slices
│   ├── Auth/                          # Authentication feature
│   │   ├── Application/
│   │   │   ├── Dtos/                  # Data Transfer Objects
│   │   │   └── Services/              # Business logic
│   │   ├── Presentation/
│   │   │   ├── Controllers/           # API Controllers
│   │   │   └── Security/              # Auth-specific security
│   │   └── AuthModule.cs              # Feature DI registration
│   │
│   ├── Voices/                        # Voice models feature
│   │   ├── Domain/
│   │   │   └── VoiceModel.cs         # Domain entity
│   │   ├── Application/
│   │   │   ├── Dtos/
│   │   │   ├── Services/
│   │   │   └── Mappers/               # Entity to DTO mapping
│   │   ├── Presentation/
│   │   │   └── Controllers/
│   │   └── VoicesModule.cs           # Feature DI registration
│   │
│   └── AudioFiles/                    # Audio files feature
│       ├── Domain/
│       │   ├── AudioFile.cs
│       │   ├── AudioPreprocessing.cs
│       │   ├── UploadStatus.cs
│       │   └── ProcessingStatus.cs
│       ├── Application/
│       │   ├── Dtos/
│       │   ├── Services/
│       │   └── Mappers/
│       ├── Presentation/
│       │   └── Controllers/
│       └── AudioFilesModule.cs
│
├── Shared/                            # Cross-cutting concerns only
│   ├── Domain/                        # Base classes and interfaces
│   │   ├── BaseAuditableEntity.cs
│   │   ├── ISoftDelete.cs
│   │   ├── IHasUserId.cs
│   │   └── ApiResponse.cs
│   ├── Interfaces/                    # Shared service interfaces
│   │   ├── ICurrentUserService.cs
│   │   ├── IDateTimeProvider.cs
│   │   ├── IS3PresignedUrlService.cs
│   │   └── ISqsService.cs
│   └── Infrastructure/                # Shared infrastructure
│       ├── Data/
│       │   ├── ApplicationDbContext.cs
│       │   ├── ModelBuilderExtensions.cs
│       │   └── Configurations/        # ALL EF Core configurations
│       ├── Services/
│       │   ├── CurrentUserService.cs
│       │   ├── S3PresignedUrlService.cs
│       │   └── SqsService.cs
│       ├── Filters/
│       │   ├── ValidationFilter.cs
│       │   └── WebhookAuthenticationAttribute.cs
│       ├── Middleware/
│       │   └── GlobalExceptionHandlerMiddleware.cs
│       └── Controllers/
│           └── BaseController.cs
│
├── Infrastructure/
│   └── DependencyInjection/
│       └── ServiceCollectionExtensions.cs  # Shared DI registration
│
└── Program.cs                         # Application entry point

VoiceByAuribus.AudioUploadNotifier/    # AWS Lambda (separate project)
├── src/
│   └── VoiceByAuribus.AudioUploadNotifier/
│       ├── Function.cs                # Lambda handler
│       └── VoiceByAuribus.AudioUploadNotifier.csproj
└── test/
    └── VoiceByAuribus.AudioUploadNotifier.Tests/
```

## Layer Responsibilities

### Domain Layer (Feature-specific)

**Purpose**: Contains business entities and domain logic

**Location**: `Features/{Feature}/Domain/`

**What belongs here**:
- Entity classes (e.g., `AudioFile`, `VoiceModel`)
- Enums (e.g., `UploadStatus`, `ProcessingStatus`)
- Value objects
- Domain-specific business logic

**Rules**:
- No dependencies on other layers
- Pure business logic only
- Can implement shared interfaces (`IHasUserId`, `ISoftDelete`)
- No infrastructure concerns

**Example**:
```csharp
namespace VoiceByAuribus_API.Features.AudioFiles.Domain;

public class AudioFile : BaseAuditableEntity, IHasUserId, ISoftDelete
{
    public Guid? UserId { get; set; }
    public required string FileName { get; set; }
    public long? FileSize { get; set; }
    public required string MimeType { get; set; }
    public required string S3Uri { get; set; }
    public UploadStatus UploadStatus { get; set; }
    public bool IsDeleted { get; set; }

    public AudioPreprocessing? Preprocessing { get; set; }
}
```

### Application Layer (Feature-specific)

**Purpose**: Contains application logic and orchestration

**Location**: `Features/{Feature}/Application/`

**What belongs here**:
- **DTOs**: Request/response models for API
- **Services**: Business logic and orchestration (interfaces + implementations)
- **Mappers**: Entity ↔ DTO mapping logic (static classes in separate files)
- **Validators**: FluentValidation validators (complex validations only)

**Rules**:
- Can depend on Domain and Shared
- No direct dependency on Infrastructure
- Services receive infrastructure via dependency injection
- Use Data Annotations for simple validations, FluentValidation for complex ones

**Example Service**:
```csharp
namespace VoiceByAuribus_API.Features.AudioFiles.Application.Services;

public class AudioFileService(
    ApplicationDbContext context,
    IS3PresignedUrlService presignedUrlService,
    IDateTimeProvider dateTimeProvider,
    IAudioPreprocessingService preprocessingService,
    IConfiguration configuration) : IAudioFileService
{
    private readonly string _audioBucket = configuration["AWS:S3:AudioFilesBucket"]
        ?? throw new InvalidOperationException("AWS:S3:AudioFilesBucket configuration is required");

    public async Task<AudioFileCreatedResponseDto> CreateAudioFileAsync(CreateAudioFileDto dto, Guid userId)
    {
        var audioFile = new AudioFile
        {
            UserId = userId,
            FileName = dto.FileName,
            FileSize = null, // Set by S3 webhook
            MimeType = dto.MimeType,
            S3Uri = BuildS3Uri(userId, Guid.NewGuid(), dto.MimeType),
            UploadStatus = UploadStatus.AwaitingUpload
        };

        context.AudioFiles.Add(audioFile);
        await context.SaveChangesAsync();

        var uploadUrl = GenerateUploadUrl(audioFile.S3Uri, dto.MimeType);

        return new AudioFileCreatedResponseDto { /* ... */ };
    }
}
```

### Presentation Layer (Feature-specific)

**Purpose**: Contains API controllers and presentation logic

**Location**: `Features/{Feature}/Presentation/`

**What belongs here**:
- Controllers (inherit from `BaseController`)
- Custom attributes/filters specific to the feature
- Request/response handling

**Rules**:
- Can depend on Application and Shared
- Controllers coordinate application services
- Return `ApiResponse<T>` for all endpoints
- Use `ICurrentUserService` for user context

**Example Controller**:
```csharp
namespace VoiceByAuribus_API.Features.AudioFiles.Presentation.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/audio-files")]
public class AudioFilesController(
    IAudioFileService audioFileService,
    ICurrentUserService currentUserService) : BaseController
{
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AudioFileCreatedResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateAudioFile([FromBody] CreateAudioFileDto dto)
    {
        var userId = currentUserService.GetUserId();
        var result = await audioFileService.CreateAudioFileAsync(dto, userId);
        return Success(result);
    }
}
```

### Feature Module (Feature-specific)

**Purpose**: Dependency injection registration for the feature

**Location**: `Features/{Feature}/{Feature}Module.cs`

**Pattern**:
```csharp
namespace VoiceByAuribus_API.Features.AudioFiles;

public static class AudioFilesModule
{
    public static IServiceCollection AddAudioFilesFeature(this IServiceCollection services)
    {
        services.AddScoped<IAudioFileService, AudioFileService>();
        services.AddScoped<IAudioPreprocessingService, AudioPreprocessingService>();
        return services;
    }
}
```

**Registration**: Called in `Program.cs` via `builder.Services.AddAudioFilesFeature()`

### Shared Infrastructure

**Purpose**: Common infrastructure used by all features

**What belongs here**:
- `ApplicationDbContext` (references all feature entities)
- EF Core entity configurations (in `Configurations/`)
- Shared services (`S3PresignedUrlService`, `SqsService`, etc.)
- Global filters and middleware
- Base controller

**Why Shared**:
- Avoid duplication across features
- Centralize database access
- Provide common utilities (S3, SQS, current user, etc.)

## Key Architectural Patterns

### 1. Vertical Slice Isolation

Each feature is self-contained and can be developed, tested, and maintained independently.

**Benefits**:
- Easy to locate all code related to a feature
- Minimal coupling between features
- New team members can focus on one feature at a time
- Features can evolve independently
- Easier to split into microservices later if needed

**Example**: The AudioFiles feature contains everything needed for audio file management - entities, services, controllers, DTOs - all in `Features/AudioFiles/`.

### 2. Dependency Inversion

Features depend on abstractions (interfaces) rather than concrete implementations.

**Example**:
```csharp
// AudioFileService depends on interface, not concrete implementation
public class AudioFileService(
    ApplicationDbContext context,
    IS3PresignedUrlService presignedUrlService,  // ← Interface
    IAudioPreprocessingService preprocessingService)  // ← Interface
{
    // Implementation
}
```

**Benefits**:
- Easier to test (mock interfaces)
- Loose coupling between components
- Easy to swap implementations

### 3. Global Query Filters

Automatic filtering applied at the DbContext level for cross-cutting concerns.

**Implemented Filters**:
- **Soft Delete**: Entities implementing `ISoftDelete` are automatically excluded from queries
- **User Ownership**: Entities implementing `IHasUserId` are scoped to current user (unless admin)

**Location**: `Shared/Infrastructure/Data/ModelBuilderExtensions.cs`

**Implementation**:
```csharp
public static void ApplyGlobalFilters(this ModelBuilder modelBuilder, IHttpContextAccessor httpContextAccessor)
{
    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
        // Soft delete filter
        if (typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
        {
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var property = Expression.Property(parameter, nameof(ISoftDelete.IsDeleted));
            var filter = Expression.Lambda(Expression.Equal(property, Expression.Constant(false)), parameter);
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }

        // User ownership filter
        if (typeof(IHasUserId).IsAssignableFrom(entityType.ClrType))
        {
            // Filter by current user unless admin
            // Implementation in ModelBuilderExtensions.cs
        }
    }
}
```

**Usage**: Filters are applied automatically. To bypass, use `IgnoreQueryFilters()`:
```csharp
var allFiles = await context.AudioFiles
    .IgnoreQueryFilters()
    .ToListAsync();
```

### 4. Auditing

Automatic tracking of entity creation and modification timestamps.

**Pattern**: Inherit from `BaseAuditableEntity`
```csharp
public class AudioFile : BaseAuditableEntity, IHasUserId
{
    // Properties
}
```

**Behavior**:
- `CreatedAt`: Set automatically on entity creation
- `UpdatedAt`: Updated automatically on entity modification
- Applied in `ApplicationDbContext.SaveChangesAsync()`

**Implementation**:
```csharp
public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    var entries = ChangeTracker.Entries<BaseAuditableEntity>();

    foreach (var entry in entries)
    {
        if (entry.State == EntityState.Added)
        {
            entry.Entity.CreatedAt = _dateTimeProvider.UtcNow;
        }

        if (entry.State == EntityState.Modified || entry.State == EntityState.Added)
        {
            entry.Entity.UpdatedAt = _dateTimeProvider.UtcNow;
        }
    }

    return await base.SaveChangesAsync(cancellationToken);
}
```

### 5. Mapping Strategy

Entity-to-DTO mapping is centralized in mapper classes.

**Location**: `Features/{Feature}/Application/Mappers/`

**Pattern**:
```csharp
namespace VoiceByAuribus_API.Features.AudioFiles.Application.Mappers;

public static class AudioFileMapper
{
    public static AudioFileResponseDto MapToResponseDto(AudioFile entity, bool isAdmin)
    {
        var dto = new AudioFileResponseDto
        {
            Id = entity.Id,
            FileName = entity.FileName,
            FileSize = entity.FileSize,
            MimeType = entity.MimeType,
            UploadStatus = entity.UploadStatus.ToString(),
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };

        // Admin-specific data included only if isAdmin == true
        if (isAdmin)
        {
            dto.S3Uri = entity.S3Uri;
            if (entity.Preprocessing is not null)
            {
                dto.Preprocessing = AudioPreprocessingMapper.MapToResponseDto(entity.Preprocessing);
            }
        }

        return dto;
    }
}
```

**Rules**:
- Mappers are static classes with static methods
- One mapper per entity
- Services call mappers, not implement mapping inline
- Admin-sensitive data conditionally included based on `isAdmin` flag

### 6. Response Standardization

All API endpoints return a standardized `ApiResponse<T>` wrapper.

**Structure**:
```json
{
  "success": true,
  "message": "Optional message",
  "data": { /* Response data */ },
  "errors": [] // Only present on validation failures
}
```

**Usage via BaseController**:
```csharp
return Success(data);  // Returns ApiResponse<T> with success=true
return Error<T>("Error message");  // Returns ApiResponse<T> with success=false
return NotFound<T>("Not found message");  // Returns 404 with ApiResponse<T>
```

**Implementation**:
```csharp
// Shared/Domain/ApiResponse.cs
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }
}

// Shared/Infrastructure/Controllers/BaseController.cs
public abstract class BaseController : ControllerBase
{
    protected IActionResult Success<T>(T data, string? message = null)
    {
        return Ok(new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data
        });
    }

    protected IActionResult Error<T>(string message, List<string>? errors = null)
    {
        return BadRequest(new ApiResponse<T>
        {
            Success = false,
            Message = message,
            Errors = errors
        });
    }
}
```

### 7. Validation Strategy

**Data Annotations** for simple validations:
```csharp
public class CreateAudioFileDto
{
    [Required(ErrorMessage = "File name is required")]
    [StringLength(255, ErrorMessage = "File name must not exceed 255 characters")]
    public required string FileName { get; set; }

    [Required(ErrorMessage = "MIME type is required")]
    [RegularExpression(@"^audio\/.*", ErrorMessage = "Only audio files are allowed")]
    public required string MimeType { get; set; }
}
```

**FluentValidation** for complex validations:
```csharp
public class ComplexValidator : AbstractValidator<ComplexDto>
{
    public ComplexValidator(IRepository repository)
    {
        // Cross-property validation
        RuleFor(x => x.StartDate)
            .LessThan(x => x.EndDate);

        // Async database validation
        RuleFor(x => x.UniqueField)
            .MustAsync(async (value, cancellation) =>
                !await repository.ExistsAsync(value));
    }
}
```

## Database Architecture

### Entity Framework Core Configuration

**DbContext Location**: `Shared/Infrastructure/Data/ApplicationDbContext.cs`

**Entity Configurations**: All `IEntityTypeConfiguration<T>` implementations MUST be in:
- `Shared/Infrastructure/Data/Configurations/`

**Naming Pattern**: `{EntityName}Configuration.cs`

**Auto-Discovery**: Configurations are automatically discovered via:
```csharp
modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
```

**Example Configuration**:
```csharp
/// <summary>
/// Entity Framework configuration for AudioFile (AudioFiles feature)
/// </summary>
public class AudioFileConfiguration : IEntityTypeConfiguration<AudioFile>
{
    public void Configure(EntityTypeBuilder<AudioFile> builder)
    {
        builder.ToTable("audio_files");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.FileSize)
            .IsRequired(false);

        builder.Property(x => x.S3Uri)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.UploadStatus)
            .IsRequired()
            .HasConversion<string>();

        // Indexes
        builder.HasIndex(x => x.S3Uri).IsUnique();
        builder.HasIndex(x => x.UserId);
    }
}
```

### Migrations

**Commands** (run from solution root):
```bash
dotnet ef migrations add MigrationName --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
dotnet ef database update --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
dotnet ef migrations remove --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
```

## Adding a New Feature

Follow these steps to add a new feature:

### 1. Create Feature Structure
```bash
Features/
  NewFeature/
    Domain/
    Application/
      Dtos/
      Services/
      Mappers/
    Presentation/
      Controllers/
    NewFeatureModule.cs
```

### 2. Create Domain Entity
```csharp
public class NewEntity : BaseAuditableEntity, IHasUserId
{
    public Guid? UserId { get; set; }
    // Properties
}
```

### 3. Create EF Configuration
- File: `Shared/Infrastructure/Data/Configurations/NewEntityConfiguration.cs`
- Add XML comment: `/// <summary>Entity Framework configuration for NewEntity (NewFeature feature)</summary>`

### 4. Create Application Services
- Interface: `INewEntityService`
- Implementation: `NewEntityService`
- DTOs in `Application/Dtos/`
- Mappers in `Application/Mappers/`

### 5. Create Controller
```csharp
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/new-feature")]
public class NewFeatureController : BaseController
{
    // Endpoints
}
```

### 6. Create Feature Module
```csharp
public static class NewFeatureModule
{
    public static IServiceCollection AddNewFeature(this IServiceCollection services)
    {
        services.AddScoped<INewEntityService, NewEntityService>();
        return services;
    }
}
```

### 7. Register in Program.cs
```csharp
builder.Services.AddNewFeature();
```

### 8. Add DbSet to ApplicationDbContext
```csharp
public DbSet<NewEntity> NewEntities => Set<NewEntity>();
```

### 9. Create Migration
```bash
dotnet ef migrations add AddNewFeature --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
```

### 10. Document Endpoints
Create `.ai_doc/v1/new-feature.md` with endpoint documentation

## Dependency Flow

```
Presentation Layer (Controllers)
        ↓
Application Layer (Services, DTOs, Mappers)
        ↓
Domain Layer (Entities, Enums)
        ↓
Shared Infrastructure (DbContext, External Services)
```

**Rules**:
- Lower layers never depend on upper layers
- All layers can depend on Shared
- Infrastructure is injected via interfaces
- Cross-feature dependencies are minimized (prefer events or shared services)

## Testing Strategy

### Unit Tests
- Test application services in isolation
- Mock infrastructure dependencies
- Focus on business logic

### Integration Tests
- Test feature endpoints end-to-end
- Use in-memory database or test database
- Verify complete request/response flow

### Lambda Tests
- Test Lambda function handlers
- Mock S3 events and HttpClient
- Verify webhook payloads

## Best Practices

1. **Feature Independence**: Minimize dependencies between features
2. **DI Registration**: All feature dependencies registered in feature module
3. **Mapper Separation**: Keep mapping logic in dedicated mapper classes
4. **BaseController Usage**: All controllers inherit from `BaseController`
5. **Global Filters**: Leverage `ISoftDelete` and `IHasUserId` for automatic filtering
6. **API Versioning**: Use URL-based versioning (`/api/v1/`)
7. **Documentation**: Document endpoints in `.ai_doc/v1/{feature}.md`
8. **Admin Data**: Conditionally include sensitive data based on `ICurrentUserService.IsAdmin`
9. **Async All The Way**: All I/O operations must be async
10. **Validation**: Use Data Annotations for simple, FluentValidation for complex

## Conventions

- **JSON**: `snake_case_lower` via `JsonNamingPolicy.SnakeCaseLower`
- **API Versioning**: URL-based (`/api/v1/auth/status`)
- **Namespaces**: Match folder structure: `VoiceByAuribus_API.Features.{Feature}.{Layer}`
- **No README files**: Documentation in `.github/` or `.ai_doc/` only
- **Database Tables**: snake_case plural (e.g., `audio_files`)
- **Enum Storage**: Convert to strings in database for readability

## Key Files Reference

| File | Purpose |
|------|---------|
| `Program.cs` | Application entry point and configuration |
| `ApplicationDbContext.cs` | EF Core database context |
| `ModelBuilderExtensions.cs` | Global query filters |
| `BaseController.cs` | Base controller with standard responses |
| `ApiResponse.cs` | Standardized response wrapper |
| `BaseAuditableEntity.cs` | Base entity with timestamps |
| `ICurrentUserService.cs` | Current user context |
| `IS3PresignedUrlService.cs` | S3 pre-signed URL generation |
| `ISqsService.cs` | SQS message publishing |

---

This architecture enables scalability, maintainability, and independent feature development while maintaining consistency across the codebase.
