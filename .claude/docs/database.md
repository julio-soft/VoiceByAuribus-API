# Database Documentation

## Overview

VoiceByAuribus API uses **PostgreSQL** as its primary database with **Entity Framework Core 10.0** as the ORM.

## Connection

### Connection String

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=voicebyauribus;Username=postgres;Password=postgres"
  }
}
```

### Local Development with Docker

```bash
cd VoiceByAuribus.API
docker-compose up -d postgres
```

**Docker Compose Configuration**:
```yaml
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_DB: voicebyauribus
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data

volumes:
  postgres_data:
```

## Database Schema

### Tables

#### voice_models

Stores voice model metadata and S3 references.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| Id | uuid | NO | Primary key |
| Name | varchar(255) | NO | Voice model display name |
| Description | text | YES | Description of the voice |
| Language | varchar(10) | NO | Language code (e.g., "en-US") |
| VoiceModelPath | varchar(1000) | NO | S3 URI to voice model file |
| VoiceModelIndexPath | varchar(1000) | NO | S3 URI to voice model index |
| CreatedAt | timestamptz | NO | Creation timestamp |
| UpdatedAt | timestamptz | NO | Last update timestamp |
| IsDeleted | boolean | NO | Soft delete flag (default: false) |

**Indexes**:
- `PK_voice_models` (PRIMARY KEY): Id
- `IX_voice_models_Name` (UNIQUE): Name

---

#### audio_files

Stores audio file metadata and upload status.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| Id | uuid | NO | Primary key |
| UserId | uuid | YES | User who owns the file |
| FileName | varchar(255) | NO | Original filename |
| FileSize | bigint | YES | File size in bytes |
| MimeType | varchar(100) | NO | MIME type (e.g., "audio/mpeg") |
| S3Uri | varchar(1000) | NO | S3 URI to uploaded file |
| UploadStatus | text | NO | Upload status enum as string |
| CreatedAt | timestamptz | NO | Creation timestamp |
| UpdatedAt | timestamptz | NO | Last update timestamp |
| IsDeleted | boolean | NO | Soft delete flag (default: false) |

**Indexes**:
- `PK_audio_files` (PRIMARY KEY): Id
- `IX_audio_files_UserId`: UserId
- `IX_audio_files_S3Uri`: S3Uri
- `IX_audio_files_UploadStatus`: UploadStatus

**UploadStatus Values**:
- `AwaitingUpload`: File record created, waiting for upload
- `Uploaded`: File successfully uploaded to S3
- `Failed`: Upload failed (not currently used)

---

#### audio_preprocessing

Stores audio preprocessing status and results.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| Id | uuid | NO | Primary key |
| AudioFileId | uuid | NO | Foreign key to audio_files |
| ProcessingStatus | text | NO | Processing status enum as string |
| S3UriShort | varchar(1000) | YES | S3 URI to 10-second preview |
| S3UriInference | varchar(1000) | YES | S3 URI to inference-ready file |
| AudioDurationSeconds | integer | YES | Audio duration in whole seconds |
| ProcessingStartedAt | timestamptz | YES | When processing started |
| ProcessingCompletedAt | timestamptz | YES | When processing completed |
| ErrorMessage | varchar(2000) | YES | Error message if failed |
| CreatedAt | timestamptz | NO | Creation timestamp |
| UpdatedAt | timestamptz | NO | Last update timestamp |
| IsDeleted | boolean | NO | Soft delete flag (default: false) |

**Indexes**:
- `PK_audio_preprocessing` (PRIMARY KEY): Id
- `IX_audio_preprocessing_AudioFileId` (UNIQUE): AudioFileId
- `IX_audio_preprocessing_ProcessingStatus`: ProcessingStatus

**Foreign Keys**:
- `FK_audio_preprocessing_audio_files_AudioFileId`: AudioFileId → audio_files(Id) ON DELETE CASCADE

**ProcessingStatus Values**:
- `Pending`: Waiting to start processing
- `Processing`: Currently being processed
- `Completed`: Successfully processed
- `Failed`: Processing failed

**Relationship**: One-to-One with audio_files (one AudioFile has zero or one AudioPreprocessing)

---

## Entity Configurations

All entity configurations are centralized in `Shared/Infrastructure/Data/Configurations/`.

### VoiceModelConfiguration.cs

```csharp
/// <summary>
/// Entity Framework configuration for VoiceModel (Voices feature)
/// </summary>
public class VoiceModelConfiguration : IEntityTypeConfiguration<VoiceModel>
{
    public void Configure(EntityTypeBuilder<VoiceModel> builder)
    {
        builder.ToTable("voice_models");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.Description)
            .HasColumnType("text");

        builder.Property(x => x.Language)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(x => x.VoiceModelPath)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.VoiceModelIndexPath)
            .IsRequired()
            .HasMaxLength(1000);

        builder.HasIndex(x => x.Name).IsUnique();
    }
}
```

### AudioFileConfiguration.cs

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

        builder.Property(x => x.MimeType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.S3Uri)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.UploadStatus)
            .IsRequired()
            .HasConversion<string>();

        builder.HasOne(x => x.Preprocessing)
            .WithOne(x => x.AudioFile)
            .HasForeignKey<AudioPreprocessing>(x => x.AudioFileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.S3Uri);
        builder.HasIndex(x => x.UploadStatus);
    }
}
```

### AudioPreprocessingConfiguration.cs

```csharp
/// <summary>
/// Entity Framework configuration for AudioPreprocessing (AudioFiles feature)
/// </summary>
public class AudioPreprocessingConfiguration : IEntityTypeConfiguration<AudioPreprocessing>
{
    public void Configure(EntityTypeBuilder<AudioPreprocessing> builder)
    {
        builder.ToTable("audio_preprocessing");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProcessingStatus)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(x => x.S3UriShort)
            .HasMaxLength(1000);

        builder.Property(x => x.S3UriInference)
            .HasMaxLength(1000);

        builder.Property(x => x.AudioDurationSeconds);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);

        builder.HasIndex(x => x.AudioFileId).IsUnique();
        builder.HasIndex(x => x.ProcessingStatus);
    }
}
```

## Global Query Filters

Global filters are automatically applied to all queries via `ModelBuilderExtensions.ApplyGlobalFilters()`.

### Soft Delete Filter

Entities implementing `ISoftDelete` are automatically excluded from queries:

```csharp
// Entities with IsDeleted = true are filtered out
var activeFiles = await context.AudioFiles.ToListAsync();
// SQL: SELECT * FROM audio_files WHERE is_deleted = false

// To include deleted entities, use IgnoreQueryFilters()
var allFiles = await context.AudioFiles.IgnoreQueryFilters().ToListAsync();
```

**Implementation**:
```csharp
if (typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
{
    var parameter = Expression.Parameter(entityType.ClrType, "e");
    var property = Expression.Property(parameter, nameof(ISoftDelete.IsDeleted));
    var filter = Expression.Lambda(
        Expression.Equal(property, Expression.Constant(false)),
        parameter
    );
    modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
}
```

### User Ownership Filter

Entities implementing `IHasUserId` are automatically scoped to the current user (unless admin):

```csharp
// Regular users only see their own files
var myFiles = await context.AudioFiles.ToListAsync();
// SQL: SELECT * FROM audio_files WHERE user_id = '{currentUserId}' AND is_deleted = false

// Admin users see all files (filter bypassed automatically)
```

**Implementation**:
```csharp
if (typeof(IHasUserId).IsAssignableFrom(entityType.ClrType))
{
    // Extract current user from HttpContext
    var userId = httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value;
    var isAdmin = httpContextAccessor.HttpContext?.User.HasClaim("scope", "voice-by-auribus-api/admin");

    if (!isAdmin && userId != null)
    {
        // Apply user filter
        var parameter = Expression.Parameter(entityType.ClrType, "e");
        var property = Expression.Property(parameter, nameof(IHasUserId.UserId));
        var userIdValue = Guid.Parse(userId);
        var filter = Expression.Lambda(
            Expression.Equal(property, Expression.Constant(userIdValue)),
            parameter
        );
        modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
    }
}
```

## Auditing

Entities inheriting from `BaseAuditableEntity` automatically get timestamp tracking:

```csharp
public abstract class BaseAuditableEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

**Automatic Behavior** (in `ApplicationDbContext.SaveChangesAsync`):
- **On INSERT**: `CreatedAt` = current UTC time
- **On UPDATE**: `UpdatedAt` = current UTC time

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

## Migrations

### Creating Migrations

From solution root:

```bash
dotnet ef migrations add MigrationName --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
```

**Naming Convention**:
- Use descriptive names: `AddAudioFilesFeature`, `UpdateAudioFileModels`
- Use PascalCase
- Describe what the migration does

### Applying Migrations

```bash
# Apply all pending migrations
dotnet ef database update --project VoiceByAuribus.API/VoiceByAuribus-API.csproj

# Apply specific migration
dotnet ef database update MigrationName --project VoiceByAuribus.API/VoiceByAuribus-API.csproj

# Rollback to specific migration
dotnet ef database update PreviousMigrationName --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
```

### Removing Migrations

```bash
# Remove last migration (only if not applied)
dotnet ef migrations remove --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
```

### Migration History

Check applied migrations:

```bash
dotnet ef migrations list --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
```

Or query the database:

```sql
SELECT * FROM public.__efmigrationshistory ORDER BY "MigrationId";
```

### Migration Files

Migrations are stored in:
- `VoiceByAuribus.API/Migrations/`

Each migration creates two files:
- `{timestamp}_{name}.cs`: Migration operations
- `{timestamp}_{name}.Designer.cs`: Model snapshot

### Example Migration

```csharp
public partial class AddAudioFilesFeature : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "audio_files",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                UserId = table.Column<Guid>(nullable: true),
                FileName = table.Column<string>(maxLength: 255, nullable: false),
                FileSize = table.Column<long>(nullable: true),
                MimeType = table.Column<string>(maxLength: 100, nullable: false),
                S3Uri = table.Column<string>(maxLength: 1000, nullable: false),
                UploadStatus = table.Column<string>(nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false),
                UpdatedAt = table.Column<DateTime>(nullable: false),
                IsDeleted = table.Column<bool>(nullable: false, defaultValue: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_audio_files", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_audio_files_UserId",
            table: "audio_files",
            column: "UserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "audio_files");
    }
}
```

## Database Context

### ApplicationDbContext

Location: `Shared/Infrastructure/Data/ApplicationDbContext.cs`

```csharp
public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    IHttpContextAccessor httpContextAccessor,
    IDateTimeProvider dateTimeProvider) : DbContext(options)
{
    // DbSets
    public DbSet<VoiceModel> VoiceModels => Set<VoiceModel>();
    public DbSet<AudioFile> AudioFiles => Set<AudioFile>();
    public DbSet<AudioPreprocessing> AudioPreprocessings => Set<AudioPreprocessing>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Auto-discover all entity configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Apply global query filters
        modelBuilder.ApplyGlobalFilters(httpContextAccessor);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Apply auditing timestamps
        var entries = ChangeTracker.Entries<BaseAuditableEntity>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = dateTimeProvider.UtcNow;
            }

            if (entry.State == EntityState.Modified || entry.State == EntityState.Added)
            {
                entry.Entity.UpdatedAt = dateTimeProvider.UtcNow;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
```

## Naming Conventions

### Database Objects

- **Tables**: `snake_case` plural (e.g., `audio_files`, `voice_models`)
- **Columns**: `snake_case` (e.g., `file_name`, `created_at`)
- **Indexes**: `IX_{table}_{column(s)}` (e.g., `IX_audio_files_UserId`)
- **Primary Keys**: `PK_{table}` (e.g., `PK_audio_files`)
- **Foreign Keys**: `FK_{child_table}_{parent_table}_{column}` (e.g., `FK_audio_preprocessing_audio_files_AudioFileId`)

### C# Code

- **Entities**: PascalCase (e.g., `AudioFile`, `VoiceModel`)
- **Properties**: PascalCase (e.g., `FileName`, `CreatedAt`)
- **DbSets**: PascalCase plural (e.g., `AudioFiles`, `VoiceModels`)

**Mapping** is handled automatically by EF Core naming conventions configured in entity configurations.

## Performance Considerations

### Indexes

Create indexes for:
- Foreign keys (automatic in many databases)
- Frequently queried columns (UserId, UploadStatus, ProcessingStatus)
- Unique constraints (Name, S3Uri)

### AsNoTracking

Use `AsNoTracking()` for read-only queries:

```csharp
// Read-only query - better performance
var files = await context.AudioFiles
    .AsNoTracking()
    .ToListAsync();

// Tracked query - needed for updates
var file = await context.AudioFiles
    .FirstOrDefaultAsync(f => f.Id == id);
file.UploadStatus = UploadStatus.Uploaded;
await context.SaveChangesAsync();
```

### Include vs Select

Use `Include` for navigation properties:

```csharp
// Load related data
var file = await context.AudioFiles
    .Include(f => f.Preprocessing)
    .FirstOrDefaultAsync(f => f.Id == id);
```

Use `Select` for projections (better performance):

```csharp
// Project only needed columns
var fileNames = await context.AudioFiles
    .Select(f => new { f.Id, f.FileName })
    .ToListAsync();
```

### Pagination

Always paginate large result sets:

```csharp
var page = 1;
var pageSize = 20;

var files = await context.AudioFiles
    .OrderByDescending(f => f.CreatedAt)
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync();
```

## Seeding Data

For development, seed data can be added in migrations or via a seed service.

### Example: Seed Voice Models

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.InsertData(
        table: "voice_models",
        columns: new[] { "Id", "Name", "Description", "Language", "VoiceModelPath", "VoiceModelIndexPath", "CreatedAt", "UpdatedAt", "IsDeleted" },
        values: new object[]
        {
            Guid.NewGuid(),
            "Default Voice",
            "Default voice model",
            "en-US",
            "s3://bucket/models/default.pth",
            "s3://bucket/models/default.index",
            DateTime.UtcNow,
            DateTime.UtcNow,
            false
        }
    );
}
```

## Backup and Restore

### Backup

```bash
# Using pg_dump
docker exec -t postgres pg_dump -U postgres voicebyauribus > backup.sql

# With compression
docker exec -t postgres pg_dump -U postgres voicebyauribus | gzip > backup.sql.gz
```

### Restore

```bash
# From SQL file
docker exec -i postgres psql -U postgres voicebyauribus < backup.sql

# From compressed file
gunzip -c backup.sql.gz | docker exec -i postgres psql -U postgres voicebyauribus
```

## Common Queries

### Check Table Sizes

```sql
SELECT
    schemaname,
    tablename,
    pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) AS size
FROM pg_tables
WHERE schemaname = 'public'
ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC;
```

### Check Indexes

```sql
SELECT
    tablename,
    indexname,
    indexdef
FROM pg_indexes
WHERE schemaname = 'public'
ORDER BY tablename, indexname;
```

### Find Deleted Records

```sql
-- Soft deleted audio files
SELECT id, file_name, created_at, updated_at
FROM audio_files
WHERE is_deleted = true;
```

### Preprocessing Status Summary

```sql
SELECT
    processing_status,
    COUNT(*) as count
FROM audio_preprocessing
WHERE is_deleted = false
GROUP BY processing_status;
```

## Troubleshooting

### Connection Issues

```bash
# Check if PostgreSQL is running
docker ps | grep postgres

# Check connection
psql -h localhost -U postgres -d voicebyauribus

# View PostgreSQL logs
docker logs postgres
```

### Migration Conflicts

If migration fails:

```bash
# Check current migration
dotnet ef migrations list --project VoiceByAuribus.API/VoiceByAuribus-API.csproj

# Rollback to previous migration
dotnet ef database update PreviousMigrationName --project VoiceByAuribus.API/VoiceByAuribus-API.csproj

# Remove failed migration
dotnet ef migrations remove --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
```

### Schema Mismatch

If entity model doesn't match database:

```bash
# Generate new migration to sync
dotnet ef migrations add SyncSchema --project VoiceByAuribus.API/VoiceByAuribus-API.csproj

# Review generated migration before applying
# Apply migration
dotnet ef database update --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
```

---

## Additional Resources

- [Entity Framework Core Documentation](https://docs.microsoft.com/en-us/ef/core/)
- [PostgreSQL Documentation](https://www.postgresql.org/docs/)
- [Architecture Documentation](architecture.md)


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