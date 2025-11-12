# Workflows Documentation

## Development Workflows

### Local Development Setup

#### 1. Clone Repository

```bash
git clone <repository-url>
cd VoiceByAuribus-API
```

#### 2. Start PostgreSQL

```bash
cd VoiceByAuribus.API
docker-compose up -d postgres
```

**Verify PostgreSQL is running**:
```bash
docker ps | grep postgres
psql -h localhost -U postgres -d voicebyauribus
```

#### 3. Configure Application

Edit `VoiceByAuribus.API/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=voicebyauribus;Username=postgres;Password=postgres"
  },
  "AWS": {
    "Region": "us-east-1",
    "S3": {
      "AudioFilesBucket": "your-dev-bucket",
      "VoiceModelsBucket": "your-dev-bucket",
      "UploadUrlExpirationMinutes": 15,
      "DownloadUrlExpirationMinutes": 720,
      "MaxFileSizeMB": 100
    },
    "SQS": {
      "PreprocessingQueueUrl": "https://sqs.us-east-1.amazonaws.com/.../dev-queue"
    }
  },
  "Webhooks": {
    "ApiKey": "dev-webhook-api-key"
  }
}
```

#### 4. Apply Migrations

```bash
cd ..  # Return to solution root
dotnet ef database update --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
```

#### 5. Run Application

```bash
# From solution root
dotnet run --project VoiceByAuribus.API/VoiceByAuribus-API.csproj

# Or from project directory
cd VoiceByAuribus.API
dotnet run
```

**Application runs on**: `http://localhost:5037`

**Swagger UI** (if enabled): `http://localhost:5037/swagger`

---

### Daily Development Workflow

#### Start Development Session

```bash
# 1. Pull latest changes
git pull origin main

# 2. Start PostgreSQL
cd VoiceByAuribus.API
docker-compose up -d postgres

# 3. Apply any new migrations
cd ..
dotnet ef database update --project VoiceByAuribus.API/VoiceByAuribus-API.csproj

# 4. Run application
dotnet run --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
```

#### During Development

**Build the solution**:
```bash
dotnet build
```

**Watch mode (auto-rebuild on changes)**:
```bash
dotnet watch run --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
```

**Clean build artifacts**:
```bash
dotnet clean
```

**Restore packages**:
```bash
dotnet restore
```

#### End Development Session

```bash
# Stop PostgreSQL (optional)
cd VoiceByAuribus.API
docker-compose down

# Or keep it running for next session
docker-compose stop
```

---

### Adding a New Feature

#### Step 1: Create Feature Structure

```bash
mkdir -p VoiceByAuribus.API/Features/NewFeature/{Domain,Application/{Dtos,Services,Mappers},Presentation/Controllers}
```

#### Step 2: Create Domain Entity

Create `Features/NewFeature/Domain/NewEntity.cs`:

```csharp
namespace VoiceByAuribus_API.Features.NewFeature.Domain;

public class NewEntity : BaseAuditableEntity, IHasUserId
{
    public Guid? UserId { get; set; }
    public required string Name { get; set; }
    // Other properties...
}
```

#### Step 3: Create EF Core Configuration

Create `Shared/Infrastructure/Data/Configurations/NewEntityConfiguration.cs`:

```csharp
/// <summary>
/// Entity Framework configuration for NewEntity (NewFeature feature)
/// </summary>
public class NewEntityConfiguration : IEntityTypeConfiguration<NewEntity>
{
    public void Configure(EntityTypeBuilder<NewEntity> builder)
    {
        builder.ToTable("new_entities");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.HasIndex(x => x.UserId);
    }
}
```

#### Step 4: Add DbSet to ApplicationDbContext

Edit `Shared/Infrastructure/Data/ApplicationDbContext.cs`:

```csharp
public DbSet<NewEntity> NewEntities => Set<NewEntity>();
```

#### Step 5: Create Migration

```bash
dotnet ef migrations add AddNewFeature --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
```

**Review the migration** in `VoiceByAuribus.API/Migrations/`:
- Check table structure
- Verify indexes
- Confirm foreign keys

**Apply migration**:
```bash
dotnet ef database update --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
```

#### Step 6: Create DTOs

Create `Features/NewFeature/Application/Dtos/`:

```csharp
// CreateNewEntityDto.cs
public class CreateNewEntityDto
{
    [Required]
    [StringLength(255)]
    public required string Name { get; set; }
}

// NewEntityResponseDto.cs
public class NewEntityResponseDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

#### Step 7: Create Mapper

Create `Features/NewFeature/Application/Mappers/NewEntityMapper.cs`:

```csharp
public static class NewEntityMapper
{
    public static NewEntityResponseDto MapToResponseDto(NewEntity entity, bool isAdmin)
    {
        return new NewEntityResponseDto
        {
            Id = entity.Id,
            Name = entity.Name,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
```

#### Step 8: Create Service

Create `Features/NewFeature/Application/Services/`:

```csharp
// INewEntityService.cs
public interface INewEntityService
{
    Task<NewEntityResponseDto> CreateAsync(CreateNewEntityDto dto, Guid userId);
    Task<NewEntityResponseDto?> GetByIdAsync(Guid id, Guid userId, bool isAdmin);
}

// NewEntityService.cs
public class NewEntityService(
    ApplicationDbContext context) : INewEntityService
{
    public async Task<NewEntityResponseDto> CreateAsync(CreateNewEntityDto dto, Guid userId)
    {
        var entity = new NewEntity
        {
            UserId = userId,
            Name = dto.Name
        };

        context.NewEntities.Add(entity);
        await context.SaveChangesAsync();

        return NewEntityMapper.MapToResponseDto(entity, false);
    }

    public async Task<NewEntityResponseDto?> GetByIdAsync(Guid id, Guid userId, bool isAdmin)
    {
        var entity = await context.NewEntities
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);

        if (entity is null) return null;

        return NewEntityMapper.MapToResponseDto(entity, isAdmin);
    }
}
```

#### Step 9: Create Controller

Create `Features/NewFeature/Presentation/Controllers/NewFeatureController.cs`:

```csharp
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/new-feature")]
public class NewFeatureController(
    INewEntityService newEntityService,
    ICurrentUserService currentUserService) : BaseController
{
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Base)]
    [ProducesResponseType(typeof(ApiResponse<NewEntityResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] CreateNewEntityDto dto)
    {
        var userId = currentUserService.GetUserId();
        var result = await newEntityService.CreateAsync(dto, userId);
        return Success(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Base)]
    [ProducesResponseType(typeof(ApiResponse<NewEntityResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<NewEntityResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id)
    {
        var userId = currentUserService.GetUserId();
        var isAdmin = currentUserService.IsAdmin;
        var result = await newEntityService.GetByIdAsync(id, userId, isAdmin);

        if (result is null)
            return NotFound<NewEntityResponseDto>("Entity not found");

        return Success(result);
    }
}
```

#### Step 10: Create Feature Module

Create `Features/NewFeature/NewFeatureModule.cs`:

```csharp
namespace VoiceByAuribus_API.Features.NewFeature;

public static class NewFeatureModule
{
    public static IServiceCollection AddNewFeature(this IServiceCollection services)
    {
        services.AddScoped<INewEntityService, NewEntityService>();
        return services;
    }
}
```

#### Step 11: Register in Program.cs

Edit `Program.cs`:

```csharp
// Add feature modules
builder.Services.AddAuthFeature();
builder.Services.AddVoicesFeature();
builder.Services.AddAudioFilesFeature();
builder.Services.AddNewFeature();  // ← Add this
```

#### Step 12: Test Endpoints

Create test file `VoiceByAuribus.API/api-tests.http`:

```http
### Create New Entity
POST {{baseUrl}}/new-feature
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "name": "Test Entity"
}

### Get Entity
GET {{baseUrl}}/new-feature/{{entityId}}
Authorization: Bearer {{token}}
```

#### Step 13: Document API

Create `.ai_doc/v1/new-feature.md` with endpoint documentation.

---

### Database Workflow

#### Creating Migrations

**After modifying entities or configurations**:

```bash
# Create migration
dotnet ef migrations add MigrationName --project VoiceByAuribus.API/VoiceByAuribus-API.csproj

# Review migration
# Check files in VoiceByAuribus.API/Migrations/

# Apply migration
dotnet ef database update --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
```

#### Rollback Migration

```bash
# List migrations
dotnet ef migrations list --project VoiceByAuribus.API/VoiceByAuribus-API.csproj

# Rollback to specific migration
dotnet ef database update PreviousMigrationName --project VoiceByAuribus.API/VoiceByAuribus-API.csproj

# Remove last migration (only if not applied)
dotnet ef migrations remove --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
```

#### Reset Database

```bash
# Drop and recreate database
docker exec -i postgres psql -U postgres -c "DROP DATABASE IF EXISTS voicebyauribus;"
docker exec -i postgres psql -U postgres -c "CREATE DATABASE voicebyauribus;"

# Apply all migrations
dotnet ef database update --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
```

---

### Testing Workflow

#### Manual API Testing

**Using REST Client extension**:

1. Install [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) in VS Code
2. Open `VoiceByAuribus.API/api-tests.http`
3. Configure variables:
   ```http
   @baseUrl = http://localhost:5037/api/v1
   @token = your-jwt-token
   ```
4. Click "Send Request" above each endpoint

**Using curl**:

```bash
# Get auth status
curl -X GET "http://localhost:5037/api/v1/auth/status" \
  -H "Authorization: Bearer ${TOKEN}"

# Create audio file
curl -X POST "http://localhost:5037/api/v1/audio-files" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"file_name":"test.mp3","mime_type":"audio/mpeg"}'
```

#### Automated Testing

```bash
# Run all tests
dotnet test VoiceByAuribus-API.sln

# Run tests for specific project
dotnet test VoiceByAuribus.AudioUploadNotifier/test/VoiceByAuribus.AudioUploadNotifier.Tests/

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

---

### Git Workflow

#### Feature Branch Workflow

```bash
# 1. Create feature branch
git checkout -b feature/new-feature

# 2. Make changes and commit
git add .
git commit -m "feat: add new feature"

# 3. Push to remote
git push origin feature/new-feature

# 4. Create pull request
# Use GitHub/GitLab UI to create PR

# 5. After approval, merge to main
# Delete feature branch
git checkout main
git pull origin main
git branch -d feature/new-feature
```

#### Commit Message Convention

Use conventional commits:

```
feat: add audio file upload feature
fix: resolve migration conflict
docs: update API documentation
refactor: extract mapper to separate file
test: add unit tests for AudioFileService
chore: update dependencies
```

#### Before Committing

```bash
# Build and verify no errors
dotnet build

# Run tests
dotnet test

# Check for pending migrations
dotnet ef migrations list --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
```

---

## Deployment Workflows

### Building for Production

```bash
# Build in Release mode
dotnet build --configuration Release

# Publish application
dotnet publish VoiceByAuribus.API/VoiceByAuribus-API.csproj \
  --configuration Release \
  --output ./publish
```

### Environment Configuration

Create environment-specific appsettings:

- `appsettings.Development.json` - Local development
- `appsettings.Staging.json` - Staging environment
- `appsettings.Production.json` - Production environment

**Production configuration**:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=prod-db-host;Database=voicebyauribus;..."
  },
  "AWS": {
    "Region": "us-east-1",
    "S3": {
      "AudioFilesBucket": "voicebyauribus-prod-audio",
      "VoiceModelsBucket": "voicebyauribus-prod-models"
    }
  }
}
```

### Database Migration in Production

```bash
# On production server
dotnet ef database update --project VoiceByAuribus.API/VoiceByAuribus-API.csproj

# Or using SQL script
dotnet ef migrations script --project VoiceByAuribus.API/VoiceByAuribus-API.csproj --output migration.sql
# Then apply SQL script manually
```

### Lambda Deployment

```bash
cd VoiceByAuribus.AudioUploadNotifier/src/VoiceByAuribus.AudioUploadNotifier

# Install Amazon.Lambda.Tools
dotnet tool install -g Amazon.Lambda.Tools

# Deploy to AWS
dotnet lambda deploy-function VoiceByAuribusAudioUploadNotifier \
  --function-role lambda-execution-role \
  --region us-east-1
```

---

## CI/CD Workflow (Example)

### GitHub Actions

Create `.github/workflows/ci-cd.yml`:

```yaml
name: CI/CD

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  build-and-test:
    runs-on: ubuntu-latest

    services:
      postgres:
        image: postgres:16
        env:
          POSTGRES_DB: voicebyauribus_test
          POSTGRES_USER: postgres
          POSTGRES_PASSWORD: postgres
        ports:
          - 5432:5432
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5

    steps:
    - uses: actions/checkout@v3

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '10.0.x'

    - name: Restore dependencies
      run: dotnet restore

    - name: Build
      run: dotnet build --configuration Release --no-restore

    - name: Test
      run: dotnet test --no-build --verbosity normal
      env:
        ConnectionStrings__DefaultConnection: "Host=localhost;Database=voicebyauribus_test;Username=postgres;Password=postgres"

    - name: Publish
      run: dotnet publish VoiceByAuribus.API/VoiceByAuribus-API.csproj --configuration Release --output ./publish

    - name: Upload artifact
      uses: actions/upload-artifact@v3
      with:
        name: api-build
        path: ./publish
```

---

## Troubleshooting Workflows

### Port Already in Use

```bash
# Find process using port 5037
lsof -ti:5037

# Kill process
lsof -ti:5037 | xargs kill -9

# Or change port in launchSettings.json
```

### Database Connection Issues

```bash
# Check PostgreSQL status
docker ps | grep postgres

# View PostgreSQL logs
docker logs postgres

# Restart PostgreSQL
docker restart postgres

# Test connection
psql -h localhost -U postgres -d voicebyauribus
```

### Migration Conflicts

```bash
# Check migration status
dotnet ef migrations list --project VoiceByAuribus.API/VoiceByAuribus-API.csproj

# If conflict, rollback and recreate
dotnet ef database update PreviousMigrationName --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
dotnet ef migrations remove --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
# Fix conflicts, then create new migration
dotnet ef migrations add FixedMigration --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
```

### Build Errors

```bash
# Clean solution
dotnet clean

# Remove bin/obj directories
find . -type d -name bin -o -name obj | xargs rm -rf

# Restore packages
dotnet restore

# Build
dotnet build
```

---

## Audio File Upload Flow

### Complete Workflow

```
1. Client → API: POST /api/v1/audio-files
   - Creates record with status "AwaitingUpload"
   - Returns pre-signed S3 upload URL

2. Client → S3: PUT to pre-signed URL
   - Uploads file directly to S3
   - S3 stores file in temp/ folder

3. S3 → Lambda: S3 Event
   - Lambda triggered on object creation
   - Extracts bucket, key, file size

4. Lambda → API: POST /api/v1/audio-files/webhook/upload-notification
   - Sends S3 URI and file size
   - Updates record to "Uploaded"
   - Triggers preprocessing

5. API → SQS: Send message
   - Queue: audio-preprocessing-queue
   - Message includes S3 paths

6. External Service → SQS: Read message
   - Downloads file from temp/ path
   - Processes audio
   - Uploads results to short/ and inference/ paths

7. External Service → API: POST /api/v1/audio-files/webhook/preprocessing-result
   - Sends processing result
   - Updates preprocessing status to "Completed"

8. Client → API: GET /api/v1/audio-files/{id}
   - Checks is_processed = true
   - Downloads processed files
```

---

## Quick Reference Commands

### Development

```bash
# Start dev environment
cd VoiceByAuribus.API && docker-compose up -d postgres && cd ..

# Run app
dotnet run --project VoiceByAuribus.API/VoiceByAuribus-API.csproj

# Watch mode
dotnet watch run --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
```

### Database

```bash
# Create migration
dotnet ef migrations add Name --project VoiceByAuribus.API/VoiceByAuribus-API.csproj

# Apply migrations
dotnet ef database update --project VoiceByAuribus.API/VoiceByAuribus-API.csproj

# List migrations
dotnet ef migrations list --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
```

### Testing

```bash
# Run all tests
dotnet test VoiceByAuribus-API.sln

# Run specific test project
dotnet test VoiceByAuribus.AudioUploadNotifier/test/VoiceByAuribus.AudioUploadNotifier.Tests/
```

### Cleanup

```bash
# Kill port
lsof -ti:5037 | xargs kill -9

# Clean build
dotnet clean

# Stop PostgreSQL
cd VoiceByAuribus.API && docker-compose down
```

---

## Additional Resources

- [Architecture Documentation](architecture.md)
- [API Documentation](api.md)
- [Database Documentation](database.md)
- [Testing Documentation](testing.md)
