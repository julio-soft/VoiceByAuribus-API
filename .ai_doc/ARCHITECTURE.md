# VoiceByAuribus API - Architecture

## Architecture Pattern: Vertical Slice + Clean Architecture Hybrid

This project follows **Vertical Slice Architecture** with Clean Architecture principles, organizing code by features rather than technical layers.

### Core Principle

> Each feature is self-contained with its own Domain, Application, and Presentation layers. No Infrastructure layer at the feature level - infrastructure concerns are shared.

## Project Structure

```
VoiceByAuribus.API/
├── Features/                           # Feature-based vertical slices
│   ├── Auth/                          # Authentication feature
│   │   ├── Domain/                    # (optional) Domain entities
│   │   ├── Application/
│   │   │   ├── Dtos/                  # Data Transfer Objects
│   │   │   ├── Services/              # Business logic
│   │   │   └── Mappers/               # Entity to DTO mapping
│   │   ├── Presentation/
│   │   │   └── Controllers/           # API Controllers
│   │   └── AuthModule.cs              # Feature DI registration
│   │
│   ├── Voices/                        # Voice models feature
│   │   ├── Domain/
│   │   │   └── VoiceModel.cs         # Domain entity
│   │   ├── Application/
│   │   │   ├── Dtos/
│   │   │   ├── Services/
│   │   │   └── Mappers/
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
│       ├── Filters/
│       ├── Middleware/
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
└── test/
    └── VoiceByAuribus.AudioUploadNotifier.Tests/
```

## Layer Responsibilities

### Domain Layer (Feature-specific)
- **Purpose**: Contains business entities and domain logic
- **Location**: `Features/{Feature}/Domain/`
- **What belongs here**:
  - Entity classes (e.g., `AudioFile`, `VoiceModel`)
  - Enums (e.g., `UploadStatus`, `ProcessingStatus`)
  - Value objects
- **Rules**:
  - No dependencies on other layers
  - Pure business logic only
  - Can implement shared interfaces (`IHasUserId`, `ISoftDelete`)

### Application Layer (Feature-specific)
- **Purpose**: Contains application logic and orchestration
- **Location**: `Features/{Feature}/Application/`
- **What belongs here**:
  - **DTOs**: Request/response models for API
  - **Services**: Business logic and orchestration (interfaces + implementations)
  - **Mappers**: Entity ↔ DTO mapping logic
- **Rules**:
  - Can depend on Domain and Shared
  - No direct dependency on Infrastructure
  - Services receive infrastructure via dependency injection

### Presentation Layer (Feature-specific)
- **Purpose**: Contains API controllers and presentation logic
- **Location**: `Features/{Feature}/Presentation/`
- **What belongs here**:
  - Controllers (inherit from `BaseController`)
  - Custom attributes/filters specific to the feature
- **Rules**:
  - Can depend on Application and Shared
  - Controllers coordinate application services
  - Return `ApiResponse<T>` for all endpoints

### Feature Module (Feature-specific)
- **Purpose**: Dependency injection registration for the feature
- **Location**: `Features/{Feature}/{Feature}Module.cs`
- **Pattern**:
```csharp
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
- **Registration**: Called in `Program.cs` via `builder.Services.AddAudioFilesFeature()`

### Shared Infrastructure
- **Purpose**: Common infrastructure used by all features
- **What belongs here**:
  - `ApplicationDbContext` (references all feature entities)
  - EF Core entity configurations (in `Configurations/`)
  - Shared services (`S3PresignedUrlService`, `SqsService`, etc.)
  - Global filters and middleware
  - Base controller

## Key Architectural Patterns

### 1. Vertical Slice Isolation
Each feature is self-contained and can be developed, tested, and maintained independently.

**Benefits**:
- Easy to locate all code related to a feature
- Minimal coupling between features
- New team members can focus on one feature at a time
- Features can evolve independently

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

### 3. Global Filters (Query Filters)
Automatic filtering applied at the DbContext level for cross-cutting concerns.

**Implemented Filters**:
- **Soft Delete**: Entities implementing `ISoftDelete` are automatically excluded from queries
- **User Ownership**: Entities implementing `IHasUserId` are scoped to current user (unless admin)

**Location**: `Shared/Infrastructure/Data/ModelBuilderExtensions.cs`

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

### 5. Mapping Strategy
Entity-to-DTO mapping is centralized in mapper classes.

**Location**: `Features/{Feature}/Application/Mappers/`

**Pattern**:
```csharp
public static class AudioFileMapper
{
    public static AudioFileResponseDto MapToResponseDto(AudioFile entity, bool isAdmin)
    {
        // Mapping logic
        // Admin-specific data included only if isAdmin == true
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
        // Configuration
    }
}
```

### Migrations

**Commands** (run from solution root):
```bash
dotnet ef migrations add MigrationName --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
dotnet ef database update --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
```

## Adding a New Feature

Follow these steps to add a new feature:

1. **Create Feature Structure**:
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

2. **Create Domain Entity**:
```csharp
public class NewEntity : BaseAuditableEntity, IHasUserId
{
    public Guid? UserId { get; set; }
    // Properties
}
```

3. **Create EF Configuration**:
- File: `Shared/Infrastructure/Data/Configurations/NewEntityConfiguration.cs`
- Add XML comment: `/// <summary>Entity Framework configuration for NewEntity (NewFeature feature)</summary>`

4. **Create Application Services**:
- Interface: `INewEntityService`
- Implementation: `NewEntityService`
- DTOs in `Application/Dtos/`
- Mappers in `Application/Mappers/`

5. **Create Controller**:
```csharp
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/new-feature")]
public class NewFeatureController : BaseController
{
    // Endpoints
}
```

6. **Create Feature Module**:
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

7. **Register in Program.cs**:
```csharp
builder.Services.AddNewFeature();
```

8. **Add DbSet to ApplicationDbContext**:
```csharp
public DbSet<NewEntity> NewEntities => Set<NewEntity>();
```

9. **Create Migration**:
```bash
dotnet ef migrations add AddNewFeature --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
```

## Dependency Flow

```
Presentation Layer (Controllers)
        ↓
Application Layer (Services, DTOs, Mappers)
        ↓
Domain Layer (Entities, Enums)
        ↓
Shared Infrastructure (DbContext, Repositories, External Services)
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
