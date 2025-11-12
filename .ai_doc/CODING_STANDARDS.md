# VoiceByAuribus API - Coding Standards

## General Principles

This document defines the coding standards, patterns, and best practices for the VoiceByAuribus API project.

## Validation Patterns

### When to Use Data Annotations

Use Data Annotations (System.ComponentModel.DataAnnotations) for **simple, straightforward validations**:

```csharp
public class CreateAudioFileDto
{
    [Required(ErrorMessage = "File name is required")]
    [StringLength(255, ErrorMessage = "File name must not exceed 255 characters")]
    public required string FileName { get; set; }

    [Required(ErrorMessage = "MIME type is required")]
    [RegularExpression(@"^audio\/.*", ErrorMessage = "Only audio files are allowed")]
    public required string MimeType { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "File size must be greater than 0")]
    public long FileSize { get; set; }
}
```

**Data Annotations are ideal for:**
- Required field validation
- String length constraints
- Range validation
- Regex patterns
- Email format
- URL format
- Single-property validations

### When to Use FluentValidation

Use FluentValidation ONLY for **complex validation scenarios**:

```csharp
public class ComplexValidator : AbstractValidator<ComplexDto>
{
    public ComplexValidator(IRepository repository)
    {
        // Cross-property validation
        RuleFor(x => x.StartDate)
            .LessThan(x => x.EndDate)
            .WithMessage("Start date must be before end date");

        // Async database validation
        RuleFor(x => x.UniqueField)
            .MustAsync(async (value, cancellation) =>
            {
                return !await repository.ExistsAsync(value);
            })
            .WithMessage("Value must be unique");

        // Conditional validation
        When(x => x.IsActive, () =>
        {
            RuleFor(x => x.ExpirationDate)
                .NotEmpty()
                .WithMessage("Expiration date required when active");
        });
    }
}
```

**FluentValidation is needed for:**
- Cross-property validation
- Async validation (database checks, external API calls)
- Conditional validation
- Complex business rules
- Dependency injection in validators

**Rule of thumb**: If validation requires external dependencies or complex logic, use FluentValidation. Otherwise, use Data Annotations.

## Mapper Patterns

### Mapper Class Structure

Mappers MUST be:
- Static classes
- In separate files (not in service classes)
- Located in `Features/{Feature}/Application/Mappers/`
- One mapper per entity

```csharp
namespace VoiceByAuribus_API.Features.AudioFiles.Application.Mappers;

/// <summary>
/// Mapper for AudioFile entity to DTOs.
/// </summary>
public static class AudioFileMapper
{
    /// <summary>
    /// Maps AudioFile entity to AudioFileResponseDto.
    /// </summary>
    /// <param name="audioFile">The audio file entity to map</param>
    /// <param name="isAdmin">Whether the current user is an admin</param>
    /// <returns>AudioFileResponseDto with appropriate data</returns>
    public static AudioFileResponseDto MapToResponseDto(AudioFile audioFile, bool isAdmin)
    {
        var dto = new AudioFileResponseDto
        {
            Id = audioFile.Id,
            FileName = audioFile.FileName,
            FileSize = audioFile.FileSize,
            MimeType = audioFile.MimeType,
            UploadStatus = audioFile.UploadStatus.ToString(),
            CreatedAt = audioFile.CreatedAt,
            UpdatedAt = audioFile.UpdatedAt
        };

        // Admin-specific data
        if (isAdmin)
        {
            dto.S3Uri = audioFile.S3Uri;
            if (audioFile.Preprocessing is not null)
            {
                dto.Preprocessing = AudioPreprocessingMapper.MapToResponseDto(audioFile.Preprocessing);
            }
        }

        return dto;
    }
}
```

### Mapper Rules

1. **Static Methods**: All mapping methods are static
2. **XML Documentation**: Document parameters and return types
3. **Admin Data**: Use `isAdmin` parameter to conditionally include sensitive data
4. **Related Entities**: Call other mappers for nested entities
5. **No Business Logic**: Mappers only transform data, no business rules

### Calling Mappers from Services

Services call mappers, never implement mapping inline:

```csharp
// ✅ Correct
public async Task<AudioFileResponseDto?> GetAudioFileByIdAsync(Guid id, Guid userId, bool isAdmin)
{
    var audioFile = await context.AudioFiles
        .Include(af => af.Preprocessing)
        .FirstOrDefaultAsync(af => af.Id == id);

    if (audioFile is null) return null;

    return AudioFileMapper.MapToResponseDto(audioFile, isAdmin);
}

// ❌ Incorrect - mapping logic in service
public async Task<AudioFileResponseDto?> GetAudioFileByIdAsync(Guid id, Guid userId, bool isAdmin)
{
    var audioFile = await context.AudioFiles.FirstOrDefaultAsync(af => af.Id == id);

    if (audioFile is null) return null;

    // Don't do this - use mapper instead
    return new AudioFileResponseDto
    {
        Id = audioFile.Id,
        FileName = audioFile.FileName,
        // ... mapping inline
    };
}
```

## DTO Patterns

### Request DTOs

```csharp
/// <summary>
/// DTO for creating an audio file.
/// </summary>
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

**Rules:**
- Use `required` keyword for non-nullable reference types
- Add Data Annotations for validation
- Provide meaningful error messages
- Use descriptive names (CreateXDto, UpdateXDto)

### Response DTOs

```csharp
/// <summary>
/// DTO for audio file responses.
/// </summary>
public class AudioFileResponseDto
{
    public Guid Id { get; set; }
    public required string FileName { get; set; }
    public long? FileSize { get; set; }
    public required string MimeType { get; set; }
    public required string UploadStatus { get; set; }
    public bool IsProcessed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Admin-only fields (populated by mapper based on isAdmin flag)
    public string? S3Uri { get; set; }
    public AudioPreprocessingResponseDto? Preprocessing { get; set; }
}
```

**Rules:**
- Use nullable types for optional fields
- Comment admin-only fields
- Include timestamps (CreatedAt, UpdatedAt)
- Convert enums to strings for JSON serialization

## Service Patterns

### Service Structure

```csharp
namespace VoiceByAuribus_API.Features.AudioFiles.Application.Services;

/// <summary>
/// Service for managing audio files.
/// </summary>
public class AudioFileService(
    ApplicationDbContext context,
    IS3PresignedUrlService presignedUrlService,
    IDateTimeProvider dateTimeProvider,
    IAudioPreprocessingService preprocessingService,
    IConfiguration configuration) : IAudioFileService
{
    // Constructor parameters are automatically assigned as fields
    // No need to manually declare private fields

    // Configuration values extracted in constructor
    private readonly string _audioBucket = configuration["AWS:S3:AudioFilesBucket"]
        ?? throw new InvalidOperationException("AWS:S3:AudioFilesBucket configuration is required");

    // Public methods implement interface
    public async Task<AudioFileResponseDto?> GetAudioFileByIdAsync(Guid id, Guid userId, bool isAdmin)
    {
        var audioFile = await context.AudioFiles
            .Include(af => af.Preprocessing)
            .AsNoTracking()
            .FirstOrDefaultAsync(af => af.Id == id && af.UserId == userId);

        if (audioFile is null) return null;

        return AudioFileMapper.MapToResponseDto(audioFile, isAdmin);
    }

    // Private helper methods
    private string BuildS3Uri(Guid userId, Guid fileId, string mimeType)
    {
        var extension = GetExtensionFromMimeType(mimeType);
        return $"s3://{_audioBucket}/audio-files/{userId}/temp/{fileId}{extension}";
    }
}
```

### Service Rules

1. **Primary Constructor**: Use primary constructor with dependency injection
2. **Interface Implementation**: All services implement an interface
3. **Configuration Validation**: Throw on missing required configuration
4. **AsNoTracking**: Use for read-only queries
5. **Include Related Data**: Use `.Include()` for navigation properties
6. **Mapper Usage**: Always use mappers for entity-to-DTO conversion
7. **Private Helpers**: Extract complex logic to private methods

## Controller Patterns

### Controller Structure

```csharp
namespace VoiceByAuribus_API.Features.AudioFiles.Presentation.Controllers;

/// <summary>
/// API endpoints for managing audio files.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/audio-files")]
public class AudioFilesController(
    IAudioFileService audioFileService,
    ICurrentUserService currentUserService) : BaseController
{
    /// <summary>
    /// Creates a new audio file and returns a pre-signed upload URL.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AudioFileCreatedResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateAudioFile([FromBody] CreateAudioFileDto dto)
    {
        var userId = currentUserService.GetUserId();
        var result = await audioFileService.CreateAudioFileAsync(dto, userId);
        return Success(result);
    }

    /// <summary>
    /// Retrieves an audio file by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AudioFileResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAudioFile([FromRoute] Guid id)
    {
        var userId = currentUserService.GetUserId();
        var isAdmin = currentUserService.IsAdmin;

        var audioFile = await audioFileService.GetAudioFileByIdAsync(id, userId, isAdmin);

        if (audioFile is null)
        {
            return NotFound<AudioFileResponseDto>("Audio file not found");
        }

        return Success(audioFile);
    }
}
```

### Controller Rules

1. **Inherit from BaseController**: All controllers extend `BaseController`
2. **API Versioning**: Use `[ApiVersion]` and versioned routes
3. **XML Documentation**: Document all endpoints
4. **ProducesResponseType**: Specify response types for Swagger
5. **Parameter Binding**: Use `[FromBody]`, `[FromRoute]`, `[FromQuery]` explicitly
6. **Success/Error Methods**: Use `Success()`, `Error()`, `NotFound()` from BaseController
7. **CurrentUser Access**: Get user info from `ICurrentUserService`
8. **Validation**: Automatic via Data Annotations or FluentValidation

## Entity Patterns

### Entity Structure

```csharp
namespace VoiceByAuribus_API.Features.AudioFiles.Domain;

/// <summary>
/// Represents an audio file uploaded by a user.
/// </summary>
public class AudioFile : BaseAuditableEntity, IHasUserId, ISoftDelete
{
    // Primary key inherited from BaseAuditableEntity (Guid Id)

    /// <summary>
    /// User who owns this audio file.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Original file name provided by the user.
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    /// File size in bytes. Set when file is uploaded to S3.
    /// </summary>
    public long? FileSize { get; set; }

    /// <summary>
    /// MIME type of the audio file (e.g., "audio/mpeg").
    /// </summary>
    public required string MimeType { get; set; }

    /// <summary>
    /// S3 URI where the file is stored (e.g., "s3://bucket/key").
    /// </summary>
    public required string S3Uri { get; set; }

    /// <summary>
    /// Current upload status.
    /// </summary>
    public UploadStatus UploadStatus { get; set; }

    /// <summary>
    /// Soft delete flag. Inherited from ISoftDelete.
    /// </summary>
    public bool IsDeleted { get; set; }

    // Navigation properties

    /// <summary>
    /// Preprocessing information for this audio file.
    /// </summary>
    public AudioPreprocessing? Preprocessing { get; set; }
}
```

### Entity Rules

1. **BaseAuditableEntity**: Inherit for automatic CreatedAt/UpdatedAt
2. **IHasUserId**: Implement for automatic user-scoped filtering
3. **ISoftDelete**: Implement for soft delete functionality
4. **XML Documentation**: Document all properties with `<summary>`
5. **Required Keyword**: Use for non-nullable reference types
6. **Nullable Types**: Use `?` for optional properties
7. **Navigation Properties**: Document relationships

### Entity Type Preferences

- **Numeric IDs**: Use `int` or `long` for simple incrementing IDs
- **Unique IDs**: Use `Guid` for distributed systems or when IDs are exposed in URLs
- **Money/Currency**: Use `decimal` with precision
- **Durations**: Use `int` for whole seconds, `TimeSpan` for complex durations
- **Status/State**: Use enums (convert to string in DTOs)

## EF Core Configuration

### Configuration File Structure

```csharp
namespace VoiceByAuribus_API.Shared.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework configuration for AudioFile (AudioFiles feature).
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

        builder.Property(x => x.MimeType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.S3Uri)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.UploadStatus)
            .IsRequired()
            .HasConversion<string>();

        // Relationship configuration
        builder.HasOne(x => x.Preprocessing)
            .WithOne(x => x.AudioFile)
            .HasForeignKey<AudioPreprocessing>(x => x.AudioFileId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for performance
        builder.HasIndex(x => x.S3Uri).IsUnique();
        builder.HasIndex(x => x.UserId);
    }
}
```

### Configuration Rules

1. **Location**: ALL configurations in `Shared/Infrastructure/Data/Configurations/`
2. **Naming**: `{EntityName}Configuration.cs`
3. **XML Comment**: Add feature name in summary
4. **Table Names**: Use snake_case (e.g., `audio_files`)
5. **Column Constraints**: Specify max length, required, etc.
6. **Enum Conversion**: Convert enums to strings for readability
7. **Relationships**: Configure foreign keys and delete behavior
8. **Indexes**: Add indexes for foreign keys and frequently queried fields

## Naming Conventions

### C# Code

- **Classes**: PascalCase (e.g., `AudioFileService`)
- **Interfaces**: IPascalCase (e.g., `IAudioFileService`)
- **Methods**: PascalCase (e.g., `CreateAudioFileAsync`)
- **Properties**: PascalCase (e.g., `FileName`)
- **Private Fields**: _camelCase (e.g., `_audioBucket`)
- **Parameters**: camelCase (e.g., `userId`)
- **Local Variables**: camelCase (e.g., `audioFile`)

### Database

- **Tables**: snake_case plural (e.g., `audio_files`)
- **Columns**: snake_case (e.g., `file_name`)
- **Foreign Keys**: `{table}_id` (e.g., `audio_file_id`)

### Files and Folders

- **Feature Folders**: PascalCase (e.g., `AudioFiles`)
- **Namespace Folders**: PascalCase (e.g., `Application`, `Domain`)
- **Files**: PascalCase matching class name (e.g., `AudioFileService.cs`)

### API Routes

- **Base Route**: `/api/v{version}/{resource}` (e.g., `/api/v1/audio-files`)
- **Resource Names**: kebab-case plural (e.g., `audio-files`)
- **Actions**: kebab-case (e.g., `/audio-files/{id}/regenerate-upload-url`)

## Async Patterns

### Always Use Async for I/O Operations

```csharp
// ✅ Correct
public async Task<AudioFileResponseDto?> GetAudioFileByIdAsync(Guid id)
{
    var audioFile = await context.AudioFiles
        .FirstOrDefaultAsync(af => af.Id == id);
    return audioFile is null ? null : AudioFileMapper.MapToResponseDto(audioFile, false);
}

// ❌ Incorrect - blocking call
public AudioFileResponseDto? GetAudioFileById(Guid id)
{
    var audioFile = context.AudioFiles
        .FirstOrDefault(af => af.Id == id); // Blocks thread
    return audioFile is null ? null : AudioFileMapper.MapToResponseDto(audioFile, false);
}
```

### Async Method Naming

- Suffix async methods with `Async`: `CreateAsync`, `GetByIdAsync`, `DeleteAsync`
- Return `Task<T>` for methods with return value
- Return `Task` for void methods

## Error Handling

### Service Layer

```csharp
public async Task<AudioFileResponseDto?> GetAudioFileByIdAsync(Guid id, Guid userId, bool isAdmin)
{
    // Return null for not found - let controller decide response
    var audioFile = await context.AudioFiles
        .FirstOrDefaultAsync(af => af.Id == id);

    if (audioFile is null) return null;

    return AudioFileMapper.MapToResponseDto(audioFile, isAdmin);
}

public async Task UpdateStatusAsync(Guid id, string status)
{
    var audioFile = await context.AudioFiles.FindAsync(id);

    // Throw for invalid operations
    if (audioFile is null)
    {
        throw new InvalidOperationException("Audio file not found");
    }

    audioFile.UploadStatus = Enum.Parse<UploadStatus>(status);
    await context.SaveChangesAsync();
}
```

### Controller Layer

```csharp
[HttpGet("{id:guid}")]
public async Task<IActionResult> GetAudioFile([FromRoute] Guid id)
{
    var audioFile = await audioFileService.GetAudioFileByIdAsync(id, userId, isAdmin);

    if (audioFile is null)
    {
        return NotFound<AudioFileResponseDto>("Audio file not found");
    }

    return Success(audioFile);
}
```

**Rules:**
- Services return `null` for not found items
- Services throw exceptions for invalid operations
- Controllers convert `null` to 404 responses
- Global exception handler catches unhandled exceptions

## Security Best Practices

### User Ownership

Always filter by `UserId` when querying user-owned resources:

```csharp
// ✅ Correct - includes user check
var audioFile = await context.AudioFiles
    .FirstOrDefaultAsync(af => af.Id == id && af.UserId == userId);

// ❌ Incorrect - missing user check (security vulnerability!)
var audioFile = await context.AudioFiles
    .FirstOrDefaultAsync(af => af.Id == id);
```

**Exception**: Admin users can access all resources (check `isAdmin` flag).

### Sensitive Data

Use `isAdmin` parameter to conditionally include sensitive data:

```csharp
if (isAdmin)
{
    dto.S3Uri = audioFile.S3Uri;
    dto.InternalMetadata = audioFile.Metadata;
}
```

### Pre-signed URLs

- Set appropriate expiration times (don't use excessively long lifetimes)
- Include size limits to prevent abuse
- Include content type restrictions

```csharp
var uploadUrl = presignedUrlService.CreateUploadUrl(
    bucket,
    key,
    lifetime: TimeSpan.FromMinutes(15),  // Short expiration
    maxSizeBytes: 100 * 1024 * 1024,     // 100 MB limit
    contentType: "audio/mpeg"             // Type restriction
);
```

## Configuration Management

### appsettings.json Structure

```json
{
  "AWS": {
    "Region": "us-east-1",
    "S3": {
      "AudioFilesBucket": "voicebyauribus-audio",
      "UploadUrlExpirationMinutes": 15,
      "MaxFileSizeMB": 100
    },
    "SQS": {
      "PreprocessingQueueUrl": "https://sqs.us-east-1.amazonaws.com/..."
    }
  }
}
```

### Reading Configuration

```csharp
// Required configuration - throw if missing
private readonly string _audioBucket = configuration["AWS:S3:AudioFilesBucket"]
    ?? throw new InvalidOperationException("AWS:S3:AudioFilesBucket configuration is required");

// Optional with default
private readonly int _uploadExpirationMinutes =
    configuration.GetValue<int>("AWS:S3:UploadUrlExpirationMinutes", 15);

// Parse to specific type
private readonly long _maxFileSizeBytes =
    configuration.GetValue<int>("AWS:S3:MaxFileSizeMB") * 1024 * 1024;
```

## Testing Patterns

### Unit Test Structure

```csharp
[Fact]
public async Task CreateAudioFileAsync_WithValidData_ReturnsCreatedResponse()
{
    // Arrange
    var dto = new CreateAudioFileDto
    {
        FileName = "test.mp3",
        MimeType = "audio/mpeg"
    };
    var userId = Guid.NewGuid();

    // Act
    var result = await _audioFileService.CreateAudioFileAsync(dto, userId);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(dto.FileName, result.FileName);
    Assert.NotNull(result.UploadUrl);
}
```

### Integration Test Pattern

```csharp
public class AudioFilesControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AudioFilesControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateAudioFile_ReturnsCreatedAudioFile()
    {
        // Arrange
        var dto = new CreateAudioFileDto { FileName = "test.mp3", MimeType = "audio/mpeg" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/audio-files", dto);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<AudioFileCreatedResponseDto>>();
        Assert.True(result.Success);
    }
}
```

## Documentation

### XML Documentation

Always document:
- Public classes
- Public methods
- Public properties
- Parameters with complex meaning
- Return values

```csharp
/// <summary>
/// Service for managing audio files.
/// </summary>
public class AudioFileService : IAudioFileService
{
    /// <summary>
    /// Creates a new audio file record and generates a pre-signed upload URL.
    /// </summary>
    /// <param name="dto">The audio file creation data</param>
    /// <param name="userId">The ID of the user creating the file</param>
    /// <returns>A response containing the created file info and upload URL</returns>
    public async Task<AudioFileCreatedResponseDto> CreateAudioFileAsync(
        CreateAudioFileDto dto,
        Guid userId)
    {
        // Implementation
    }
}
```

### Code Comments

Use comments for:
- Complex business logic
- Non-obvious decisions
- Temporary workarounds (with TODO)

```csharp
// Store file size as null initially - will be set by S3 webhook
audioFile.FileSize = null;

// TODO: Add support for resumable uploads
```

Avoid comments for:
- Obvious code
- Redundant information

## Common Patterns Summary

| Pattern | Use |
|---------|-----|
| Data Annotations | Simple validations (required, length, regex) |
| FluentValidation | Complex validations (cross-property, async, conditional) |
| Static Mappers | Entity ↔ DTO conversion in separate files |
| Primary Constructor | Service and controller dependency injection |
| BaseAuditableEntity | Automatic CreatedAt/UpdatedAt timestamps |
| IHasUserId | Automatic user-scoped filtering |
| ISoftDelete | Soft delete functionality |
| ApiResponse<T> | Standardized API responses |
| Async/Await | All I/O operations (database, HTTP, S3) |
| AsNoTracking | Read-only queries for performance |

---

**Remember**: Consistency is key. Follow these patterns throughout the codebase to maintain readability and maintainability.
