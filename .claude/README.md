# VoiceByAuribus API

## Project Overview

VoiceByAuribus API is a .NET-based backend service for voice model management, audio file processing, and voice conversion. The API provides:

- **Voice Model Management**: CRUD operations for voice models with S3 storage integration
- **Audio File Upload & Processing**: Pre-signed URL-based uploads with automatic preprocessing pipeline
- **Voice Conversion**: Background-processed voice transformations with pitch shifting and status tracking
- **Authentication**: AWS Cognito M2M (machine-to-machine) authentication with scope-based authorization
- **Multi-tenancy**: User-owned resources with automatic filtering and admin override
- **Health Monitoring**: Comprehensive health checks for services, database, and AWS resources

## Technology Stack

### Core Framework
- **.NET 10.0**: Latest LTS version of .NET
- **ASP.NET Core**: Web API framework
- **Entity Framework Core 10.0**: ORM for PostgreSQL

### Database
- **PostgreSQL**: Primary database
- **EF Core Migrations**: Database schema management

### AWS Services
- **S3**: Object storage for audio files and voice models
- **SQS**: Queues for audio preprocessing and voice inference tasks
- **Lambda**: Event-driven functions (S3 upload notifications)
- **Cognito**: M2M authentication with resource server scopes
- **Secrets Manager**: Secure configuration management with automatic loading

### Additional Libraries
- **FluentValidation**: Complex request validation
- **Asp.Versioning**: URL-based API versioning
- **AWSSDK.S3, AWSSDK.SQS, AWSSDK.SecretsManager**: AWS service integration
- **Serilog**: Structured logging with JSON output

## Architecture

This project follows **Vertical Slice Architecture** with Clean Architecture principles. Each feature is self-contained with its own Domain, Application, and Presentation layers.

```
VoiceByAuribus.API/
├── Features/                    # Feature-based vertical slices
│   ├── Auth/                    # Authentication feature
│   │   ├── Application/
│   │   ├── Presentation/
│   │   └── AuthModule.cs        # Feature DI registration
│   │
│   ├── Voices/                  # Voice models feature
│   │   ├── Domain/
│   │   ├── Application/
│   │   ├── Presentation/
│   │   └── VoicesModule.cs
│   │
│   ├── AudioFiles/              # Audio files feature
│   │   ├── Domain/
│   │   ├── Application/
│   │   ├── Presentation/
│   │   └── AudioFilesModule.cs
│   │
│   └── VoiceConversions/        # Voice conversion feature
│       ├── Domain/
│       ├── Application/
│       ├── Presentation/
│       └── VoiceConversionsModule.cs
│
├── Shared/                      # Cross-cutting concerns
│   ├── Domain/                  # Base entities and response models
│   ├── Interfaces/              # Shared service interfaces
│   └── Infrastructure/
│       ├── Data/                # DbContext and configurations
│       ├── Services/            # Shared services
│       ├── Filters/             # Global filters
│       ├── Middleware/          # Global middleware
│       └── Controllers/         # Base controller
│
└── Program.cs                   # Application entry point

VoiceByAuribus.AudioUploadNotifier/  # AWS Lambda project
├── src/
└── test/
```

### Key Architectural Patterns

1. **Vertical Slice Isolation**: Each feature is self-contained and independent
2. **Dependency Inversion**: Features depend on interfaces, not implementations
3. **Global Query Filters**: Automatic soft-delete and user ownership filtering
4. **Auditing**: Automatic CreatedAt/UpdatedAt timestamps
5. **Standardized Responses**: All endpoints return `ApiResponse<T>` wrapper
6. **Background Processing**: Hosted services with optimistic locking for concurrent operations
7. **Optimistic Concurrency**: Row versioning for safe multi-instance deployments

**See**: [docs/architecture.md](docs/architecture.md) for detailed architecture documentation

## Project Structure

### Solution Organization

```
VoiceByAuribus-API.sln           # Solution file at root
├── VoiceByAuribus.API/          # Main API project
│   └── VoiceByAuribus-API.csproj
└── VoiceByAuribus.AudioUploadNotifier/   # Lambda function
    ├── src/VoiceByAuribus.AudioUploadNotifier/
    └── test/VoiceByAuribus.AudioUploadNotifier.Tests/
```

### Feature Structure

Each feature follows this pattern:

```
Features/FeatureName/
├── Domain/                      # Entities, enums, value objects
├── Application/
│   ├── Dtos/                    # Request/response DTOs
│   ├── Services/                # Business logic services
│   ├── Mappers/                 # Entity-to-DTO mapping
│   └── Validators/              # FluentValidation validators (complex only)
├── Presentation/
│   └── Controllers/             # API controllers
└── FeatureNameModule.cs         # DI registration
```

## Environment Setup

### Prerequisites

- **.NET 10.0 SDK**: [Download](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Docker**: For local PostgreSQL
- **AWS CLI**: For AWS service integration (optional for local dev)
- **IDE**: Visual Studio, VS Code, or Rider

### Local Development Setup

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd VoiceByAuribus-API
   ```

2. **Start PostgreSQL with Docker**
   ```bash
   cd VoiceByAuribus.API
   docker-compose up -d postgres
   ```

3. **Configure appsettings.json**
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Database=voicebyauribus;Username=postgres;Password=postgres"
     },
     "AWS": {
       "Region": "us-east-1",
       "S3": {
         "AudioFilesBucket": "your-bucket-name",
         "UploadUrlExpirationMinutes": 15,
         "MaxFileSizeMB": 100
       },
       "SQS": {
         "AudioPreprocessingQueue": "aurivoice-svs-prep-nbl.fifo",
         "VoiceInferenceQueue": "voice-inference-queue",
         "PreviewInferenceQueue": "preview-inference-queue"
       }
     }
   }
   ```

4. **Run migrations**
   ```bash
   cd ..  # Return to solution root
   dotnet ef database update --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
   ```

5. **Run the application**
   ```bash
   dotnet run --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
   # Runs on http://localhost:5037
   ```

## Common Commands

### Build & Run

```bash
# From solution root
dotnet build                                              # Build all projects
dotnet build VoiceByAuribus-API.sln                      # Build solution explicitly
dotnet run --project VoiceByAuribus.API/VoiceByAuribus-API.csproj

# From project directory
cd VoiceByAuribus.API
dotnet run                                               # Runs on http://localhost:5037
```

### Database Migrations

```bash
# From solution root
dotnet ef migrations add MigrationName --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
dotnet ef database update --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
dotnet ef migrations remove --project VoiceByAuribus.API/VoiceByAuribus-API.csproj
```

### Testing

```bash
# Test API with REST Client
# Use VoiceByAuribus.API/api-tests.http with REST Client extension

# Test Lambda function
dotnet test VoiceByAuribus.AudioUploadNotifier/test/VoiceByAuribus.AudioUploadNotifier.Tests/

# Test entire solution
dotnet test VoiceByAuribus-API.sln
```

### Troubleshooting

```bash
# Kill port conflicts
lsof -ti:5037 | xargs kill -9

# Clean build artifacts
dotnet clean

# Restore packages
dotnet restore
```

## Code Conventions

### Naming Conventions

- **C# Code**: PascalCase for classes, methods, properties
- **Private Fields**: _camelCase with underscore prefix
- **Parameters/Variables**: camelCase
- **Database Tables**: snake_case plural (e.g., `audio_files`)
- **API Routes**: kebab-case (e.g., `/api/v1/audio-files`)
- **JSON**: snake_case_lower (configured via `JsonNamingPolicy`)

### Validation

- **Data Annotations**: Use for simple validations (required, length, regex)
- **FluentValidation**: Use ONLY for complex validations (cross-property, async, conditional)

### Response Format

All endpoints return `ApiResponse<T>`:

```json
{
  "success": true,
  "message": "Optional message",
  "data": { /* Response data */ },
  "errors": []  // Only present on validation failures
}
```

### Async Patterns

- All I/O operations must be async
- Method names end with `Async`
- Return `Task<T>` or `Task`

### Documentation

- **XML Comments**: Required for all public classes, methods, and properties
- **Endpoint Docs**: Maintained in `.ai_doc/v1/` directory
- **No README files**: Documentation goes in `.github/` or `.ai_doc/` only

**See**: [docs/architecture.md](docs/architecture.md) for detailed coding standards

## Configuration

### Key Configuration Areas

The application is configured via `appsettings.json` and AWS Secrets Manager (in production):

- **Serilog**: Structured logging with JSON output (replaces default .NET logging)
- **Database**: PostgreSQL connection string
- **Authentication**: AWS Cognito M2M with custom scope validation
- **AWS Services**: S3 buckets, SQS queue names (not URLs), region configuration
- **Webhooks**: API key authentication for internal webhooks
- **VoiceConversions**: Background processor settings (interval, retry logic, timeouts)
- **AWS Secrets Manager**: Auto-loaded in production via custom configuration provider

**See**: `VoiceByAuribus.API/appsettings.json` for complete configuration structure

## Authentication

The API uses **AWS Cognito M2M authentication** with scope-based authorization.

### Key Points

- **M2M Tokens**: Cognito M2M tokens don't include standard `aud` claim
- **Validation**: `ValidateAudience=false` with custom scope validation
- **Scopes**:
  - `voice-by-auribus-api/base`: Read access
  - `voice-by-auribus-api/admin`: Admin access (internal paths exposed)
- **Current User**: `ICurrentUserService` extracts claims from JWT

**See**: `.ai_doc/COGNITO_M2M_AUTH.md` for detailed authentication documentation

## Features

### Auth Feature
- Token validation endpoint
- Cognito M2M integration
- Scope-based authorization policies

### Voices Feature
- CRUD operations for voice models
- S3 integration for voice model files
- Admin-only file paths exposure
- Pre-signed URLs for downloads

### AudioFiles Feature
- Audio file upload via pre-signed URLs
- S3 event-driven upload notifications (Lambda)
- Automatic preprocessing pipeline (SQS)
- Processing status tracking
- User ownership and soft delete

### VoiceConversions Feature ⭐ NEW
- Voice-to-voice conversion with pitch shifting
- Background processor with optimistic locking (3-second polling)
- Automatic status progression based on preprocessing completion
- Retry mechanism with exponential backoff (max 5 attempts)
- Support for full audio and preview (10s) conversions
- Multiple transposition options (same octave, ±12 semitones, thirds, fifths)
- Health monitoring integration
- Webhook-based result handling from external inference service
- User ownership with automatic filtering

**See**: [docs/api.md](docs/api.md) for detailed API documentation

## Key Files Reference

### Entry Point
- `Program.cs`: Application configuration and startup

### Feature Modules
- `Features/Auth/AuthModule.cs`: Auth DI registration
- `Features/Voices/VoicesModule.cs`: Voices DI registration
- `Features/AudioFiles/AudioFilesModule.cs`: AudioFiles DI registration
- `Features/VoiceConversions/VoiceConversionsModule.cs`: VoiceConversions DI registration

### Shared Infrastructure
- `Shared/Infrastructure/Data/ApplicationDbContext.cs`: EF Core DbContext
- `Shared/Infrastructure/Data/ModelBuilderExtensions.cs`: Global query filters
- `Shared/Infrastructure/Controllers/BaseController.cs`: Base controller with standard responses
- `Shared/Infrastructure/Services/CurrentUserService.cs`: JWT claims extraction
- `Shared/Infrastructure/Services/S3PresignedUrlService.cs`: S3 URL generation
- `Shared/Infrastructure/Services/SqsService.cs`: SQS message publishing
- `Shared/Infrastructure/Services/SqsQueueResolver.cs`: Queue name to URL resolution with caching
- `Shared/Infrastructure/Services/HealthCheckService.cs`: Comprehensive health monitoring
- `Shared/Infrastructure/Configuration/AwsSecretsManagerConfigurationProvider.cs`: AWS Secrets Manager integration
- `Shared/Infrastructure/Filters/WebhookAuthenticationAttribute.cs`: Webhook API key validation
- `Shared/Infrastructure/Middleware/GlobalExceptionHandlerMiddleware.cs`: Global error handling

### Domain
- `Shared/Domain/BaseAuditableEntity.cs`: Base entity with timestamps
- `Shared/Domain/ApiResponse.cs`: Standardized API response wrapper

### Lambda
- `VoiceByAuribus.AudioUploadNotifier/src/VoiceByAuribus.AudioUploadNotifier/Function.cs`: S3 upload event handler

## Additional Documentation

- [Architecture Details](docs/architecture.md) - Comprehensive architecture patterns and guidelines
- [API Documentation](docs/api.md) - Complete API endpoint reference
- [Database Schema](docs/database.md) - Database design and migrations
- [Workflows](docs/workflows.md) - Development and deployment workflows
- [Testing Guide](docs/testing.md) - Testing strategies and examples

## AWS Resources

For AWS infrastructure details, see `.ai_doc/AWS_RESOURCES.md`.

## Contributing

When adding new features:

1. Create feature structure in `Features/NewFeature/`
2. Create entity in `Domain/`
3. Create EF configuration in `Shared/Infrastructure/Data/Configurations/`
4. Create services and DTOs in `Application/`
5. Create controllers in `Presentation/`
6. Create module with DI registration: `NewFeatureModule.cs`
7. Register in `Program.cs`: `builder.Services.AddNewFeature()`
8. Create migration: `dotnet ef migrations add AddNewFeature`
9. Document endpoints in `.ai_doc/v1/new-feature.md`

**See**: [docs/architecture.md](docs/architecture.md) for detailed feature creation guide

## License

[Your license information]

## Support

[Your support information]
