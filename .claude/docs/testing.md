# Testing Documentation

## Overview

This document covers testing strategies, patterns, and best practices for the VoiceByAuribus API project.

## Testing Strategy

### Test Pyramid

```
         /\
        /  \  E2E Tests (Few)
       /----\
      /      \  Integration Tests (Some)
     /--------\
    /          \  Unit Tests (Many)
   /____________\
```

**Distribution**:
- **Unit Tests (70%)**: Test business logic in isolation
- **Integration Tests (20%)**: Test feature endpoints end-to-end
- **E2E Tests (10%)**: Test complete user workflows

## Test Projects

### Structure

```
VoiceByAuribus-API.sln
├── VoiceByAuribus.API/
│   └── VoiceByAuribus-API.csproj
├── VoiceByAuribus.API.Tests/              # Main API tests
│   ├── Unit/
│   │   ├── Features/
│   │   │   ├── Auth/
│   │   │   ├── Voices/
│   │   │   └── AudioFiles/
│   │   └── Shared/
│   ├── Integration/
│   │   └── Features/
│   └── VoiceByAuribus.API.Tests.csproj
└── VoiceByAuribus.AudioUploadNotifier/
    └── test/
        └── VoiceByAuribus.AudioUploadNotifier.Tests/
```

## Unit Testing

### Testing Framework

- **xUnit**: Test framework
- **Moq**: Mocking framework
- **FluentAssertions**: Assertion library

### Example: Testing Services

#### AudioFileService Unit Tests

```csharp
using Moq;
using Xunit;
using FluentAssertions;
using VoiceByAuribus_API.Features.AudioFiles.Application.Services;
using VoiceByAuribus_API.Features.AudioFiles.Application.Dtos;

namespace VoiceByAuribus.API.Tests.Unit.Features.AudioFiles;

public class AudioFileServiceTests
{
    private readonly Mock<ApplicationDbContext> _contextMock;
    private readonly Mock<IS3PresignedUrlService> _presignedUrlServiceMock;
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock;
    private readonly Mock<IAudioPreprocessingService> _preprocessingServiceMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly AudioFileService _sut; // System Under Test

    public AudioFileServiceTests()
    {
        _contextMock = new Mock<ApplicationDbContext>();
        _presignedUrlServiceMock = new Mock<IS3PresignedUrlService>();
        _dateTimeProviderMock = new Mock<IDateTimeProvider>();
        _preprocessingServiceMock = new Mock<IAudioPreprocessingService>();
        _configurationMock = new Mock<IConfiguration>();

        // Setup configuration
        _configurationMock.Setup(c => c["AWS:S3:AudioFilesBucket"])
            .Returns("test-bucket");
        _configurationMock.Setup(c => c.GetValue<int>("AWS:S3:UploadUrlExpirationMinutes", It.IsAny<int>()))
            .Returns(15);
        _configurationMock.Setup(c => c.GetValue<int>("AWS:S3:MaxFileSizeMB", It.IsAny<int>()))
            .Returns(100);

        _dateTimeProviderMock.Setup(d => d.UtcNow)
            .Returns(new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc));

        _sut = new AudioFileService(
            _contextMock.Object,
            _presignedUrlServiceMock.Object,
            _dateTimeProviderMock.Object,
            _preprocessingServiceMock.Object,
            _configurationMock.Object
        );
    }

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
        var uploadUrl = "https://s3.amazonaws.com/test-bucket/path?signature=...";

        _presignedUrlServiceMock
            .Setup(s => s.CreateUploadUrl(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<long>(), It.IsAny<string>()))
            .Returns(uploadUrl);

        // Act
        var result = await _sut.CreateAudioFileAsync(dto, userId);

        // Assert
        result.Should().NotBeNull();
        result.FileName.Should().Be(dto.FileName);
        result.MimeType.Should().Be(dto.MimeType);
        result.UploadStatus.Should().Be("AwaitingUpload");
        result.UploadUrl.Should().Be(uploadUrl);
        result.Id.Should().NotBeEmpty();

        _contextMock.Verify(c => c.AudioFiles.Add(It.IsAny<AudioFile>()), Times.Once);
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegenerateUploadUrlAsync_WithAwaitingUploadStatus_ReturnsNewUrl()
    {
        // Arrange
        var audioFileId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var audioFile = new AudioFile
        {
            Id = audioFileId,
            UserId = userId,
            FileName = "test.mp3",
            MimeType = "audio/mpeg",
            S3Uri = "s3://test-bucket/path/file.mp3",
            UploadStatus = UploadStatus.AwaitingUpload
        };

        var mockDbSet = CreateMockDbSet(new[] { audioFile });
        _contextMock.Setup(c => c.AudioFiles).Returns(mockDbSet.Object);

        var newUploadUrl = "https://s3.amazonaws.com/test-bucket/path?signature=new";
        _presignedUrlServiceMock
            .Setup(s => s.CreateUploadUrl(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<long>(), It.IsAny<string>()))
            .Returns(newUploadUrl);

        // Act
        var result = await _sut.RegenerateUploadUrlAsync(audioFileId, userId);

        // Assert
        result.Should().NotBeNull();
        result.UploadUrl.Should().Be(newUploadUrl);
        result.UploadUrlExpiresAt.Should().BeAfter(_dateTimeProviderMock.Object.UtcNow);
    }

    [Fact]
    public async Task RegenerateUploadUrlAsync_WithUploadedStatus_ThrowsInvalidOperationException()
    {
        // Arrange
        var audioFileId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var audioFile = new AudioFile
        {
            Id = audioFileId,
            UserId = userId,
            FileName = "test.mp3",
            MimeType = "audio/mpeg",
            S3Uri = "s3://test-bucket/path/file.mp3",
            UploadStatus = UploadStatus.Uploaded // Already uploaded
        };

        var mockDbSet = CreateMockDbSet(new[] { audioFile });
        _contextMock.Setup(c => c.AudioFiles).Returns(mockDbSet.Object);

        // Act
        Func<Task> act = async () => await _sut.RegenerateUploadUrlAsync(audioFileId, userId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Upload URL can only be regenerated for files awaiting upload");
    }

    [Fact]
    public async Task HandleUploadNotificationAsync_UpdatesStatusAndTriggersPreprocessing()
    {
        // Arrange
        var s3Uri = "s3://test-bucket/audio-files/user123/temp/file.mp3";
        var fileSize = 5242880L;
        var audioFile = new AudioFile
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            FileName = "test.mp3",
            MimeType = "audio/mpeg",
            S3Uri = s3Uri,
            UploadStatus = UploadStatus.AwaitingUpload,
            FileSize = null
        };

        var mockDbSet = CreateMockDbSet(new[] { audioFile });
        _contextMock.Setup(c => c.AudioFiles).Returns(mockDbSet.Object);

        // Act
        await _sut.HandleUploadNotificationAsync(s3Uri, fileSize);

        // Assert
        audioFile.UploadStatus.Should().Be(UploadStatus.Uploaded);
        audioFile.FileSize.Should().Be(fileSize);
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _preprocessingServiceMock.Verify(p => p.TriggerPreprocessingAsync(audioFile.Id), Times.Once);
    }

    private Mock<DbSet<T>> CreateMockDbSet<T>(IEnumerable<T> data) where T : class
    {
        var queryableData = data.AsQueryable();
        var mockSet = new Mock<DbSet<T>>();

        mockSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(queryableData.Provider);
        mockSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryableData.Expression);
        mockSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryableData.ElementType);
        mockSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(queryableData.GetEnumerator());

        return mockSet;
    }
}
```

### Testing Mappers

```csharp
public class AudioFileMapperTests
{
    [Fact]
    public void MapToResponseDto_WithRegularUser_ExcludesAdminData()
    {
        // Arrange
        var audioFile = new AudioFile
        {
            Id = Guid.NewGuid(),
            FileName = "test.mp3",
            FileSize = 5242880,
            MimeType = "audio/mpeg",
            S3Uri = "s3://bucket/path/file.mp3",
            UploadStatus = UploadStatus.Uploaded,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var result = AudioFileMapper.MapToResponseDto(audioFile, isAdmin: false);

        // Assert
        result.Id.Should().Be(audioFile.Id);
        result.FileName.Should().Be(audioFile.FileName);
        result.S3Uri.Should().BeNull(); // Admin-only field
        result.Preprocessing.Should().BeNull(); // Admin-only field
    }

    [Fact]
    public void MapToResponseDto_WithAdmin_IncludesAdminData()
    {
        // Arrange
        var audioFile = new AudioFile
        {
            Id = Guid.NewGuid(),
            FileName = "test.mp3",
            FileSize = 5242880,
            MimeType = "audio/mpeg",
            S3Uri = "s3://bucket/path/file.mp3",
            UploadStatus = UploadStatus.Uploaded,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Preprocessing = new AudioPreprocessing
            {
                ProcessingStatus = ProcessingStatus.Completed,
                AudioDurationSeconds = 120
            }
        };

        // Act
        var result = AudioFileMapper.MapToResponseDto(audioFile, isAdmin: true);

        // Assert
        result.S3Uri.Should().Be(audioFile.S3Uri);
        result.Preprocessing.Should().NotBeNull();
        result.Preprocessing!.Status.Should().Be("Completed");
    }
}
```

### Testing Validators (FluentValidation)

```csharp
public class ComplexValidatorTests
{
    private readonly ComplexValidator _validator;

    public ComplexValidatorTests()
    {
        _validator = new ComplexValidator();
    }

    [Fact]
    public async Task Validate_WithValidData_ReturnsNoErrors()
    {
        // Arrange
        var dto = new ComplexDto
        {
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(7)
        };

        // Act
        var result = await _validator.ValidateAsync(dto);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Validate_WithEndDateBeforeStartDate_ReturnsError()
    {
        // Arrange
        var dto = new ComplexDto
        {
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(-7) // Invalid
        };

        // Act
        var result = await _validator.ValidateAsync(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Contain("End date must be after start date");
    }
}
```

## Integration Testing

### Setup

Integration tests use a test database and test the complete request/response flow.

```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace VoiceByAuribus.API.Tests.Integration;

public class AudioFilesControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public AudioFilesControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove the app's DbContext registration
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add in-memory database for testing
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDb");
                });

                // Build service provider and seed database
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Database.EnsureCreated();
            });
        });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task CreateAudioFile_WithValidData_ReturnsCreatedAudioFile()
    {
        // Arrange
        var request = new
        {
            file_name = "test.mp3",
            mime_type = "audio/mpeg"
        };

        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/audio-files", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<AudioFileCreatedResponseDto>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.FileName.Should().Be(request.file_name);
        result.Data.UploadUrl.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateAudioFile_WithInvalidMimeType_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            file_name = "test.txt",
            mime_type = "text/plain" // Invalid - not audio
        };

        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/audio-files", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Only audio files are allowed"));
    }

    [Fact]
    public async Task GetAudioFile_WithValidId_ReturnsAudioFile()
    {
        // Arrange
        var audioFileId = await CreateTestAudioFileAsync();
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync($"/api/v1/audio-files/{audioFileId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<AudioFileResponseDto>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Id.Should().Be(audioFileId);
    }

    [Fact]
    public async Task GetAudioFile_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var invalidId = Guid.NewGuid();
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync($"/api/v1/audio-files/{invalidId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<AudioFileResponseDto>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    private async Task<string> GetAuthTokenAsync()
    {
        // Mock implementation - replace with actual token generation
        return "mock-jwt-token";
    }

    private async Task<Guid> CreateTestAudioFileAsync()
    {
        // Helper method to create test data
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var audioFile = new AudioFile
        {
            UserId = Guid.NewGuid(),
            FileName = "test.mp3",
            MimeType = "audio/mpeg",
            S3Uri = "s3://test-bucket/path/file.mp3",
            UploadStatus = UploadStatus.AwaitingUpload
        };

        context.AudioFiles.Add(audioFile);
        await context.SaveChangesAsync();

        return audioFile.Id;
    }
}
```

## Lambda Function Testing

### Testing AWS Lambda Handler

```csharp
using Amazon.Lambda.S3Events;
using Amazon.Lambda.TestUtilities;
using Moq;
using Moq.Protected;
using Xunit;

namespace VoiceByAuribus.AudioUploadNotifier.Tests;

public class FunctionTests
{
    [Fact]
    public async Task FunctionHandler_WithValidS3Event_CallsBackendApi()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"success\":true}")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var function = new Function(httpClient, "https://api.test.com", "test-api-key");

        var s3Event = new S3Event
        {
            Records = new List<S3Event.S3EventNotificationRecord>
            {
                new S3Event.S3EventNotificationRecord
                {
                    S3 = new S3Event.S3Entity
                    {
                        Bucket = new S3Event.S3BucketEntity { Name = "test-bucket" },
                        Object = new S3Event.S3ObjectEntity
                        {
                            Key = "audio-files/user123/temp/file.mp3",
                            Size = 5242880
                        }
                    }
                }
            }
        };

        var context = new TestLambdaContext();

        // Act
        await function.FunctionHandler(s3Event, context);

        // Assert
        mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.ToString().Contains("/webhook/upload-notification") &&
                req.Headers.Contains("X-Webhook-Api-Key")
            ),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact]
    public async Task FunctionHandler_WithApiError_ThrowsHttpRequestException()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("{\"success\":false,\"message\":\"Error\"}")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var function = new Function(httpClient, "https://api.test.com", "test-api-key");

        var s3Event = new S3Event
        {
            Records = new List<S3Event.S3EventNotificationRecord>
            {
                new S3Event.S3EventNotificationRecord
                {
                    S3 = new S3Event.S3Entity
                    {
                        Bucket = new S3Event.S3BucketEntity { Name = "test-bucket" },
                        Object = new S3Event.S3ObjectEntity
                        {
                            Key = "audio-files/user123/temp/file.mp3",
                            Size = 5242880
                        }
                    }
                }
            }
        };

        var context = new TestLambdaContext();

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await function.FunctionHandler(s3Event, context);
        });
    }
}
```

## Manual Testing

### Using REST Client (VS Code)

Create `api-tests.http`:

```http
@baseUrl = http://localhost:5037/api/v1
@token = your-jwt-token-here

### Get Auth Status
GET {{baseUrl}}/auth/status
Authorization: Bearer {{token}}

### Create Audio File
POST {{baseUrl}}/audio-files
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "file_name": "test-audio.mp3",
  "mime_type": "audio/mpeg"
}

### Get Audio File
@audioFileId = 3fa85f64-5717-4562-b3fc-2c963f66afa6
GET {{baseUrl}}/audio-files/{{audioFileId}}
Authorization: Bearer {{token}}

### List Audio Files
GET {{baseUrl}}/audio-files?page=1&page_size=10
Authorization: Bearer {{token}}

### Delete Audio File
DELETE {{baseUrl}}/audio-files/{{audioFileId}}
Authorization: Bearer {{token}}

### Webhook - Upload Notification
POST {{baseUrl}}/audio-files/webhook/upload-notification
X-Webhook-Api-Key: your-webhook-api-key
Content-Type: application/json

{
  "s3_uri": "s3://bucket/audio-files/user123/temp/file.mp3",
  "file_size": 5242880
}
```

### Using Postman

1. Create collection: "VoiceByAuribus API"
2. Set collection variables:
   - `baseUrl`: `http://localhost:5037/api/v1`
   - `token`: Your JWT token
3. Add requests for each endpoint
4. Use collection runner for automated testing

### Using curl

```bash
# Get auth status
curl -X GET "http://localhost:5037/api/v1/auth/status" \
  -H "Authorization: Bearer ${TOKEN}"

# Create audio file
curl -X POST "http://localhost:5037/api/v1/audio-files" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "file_name": "test.mp3",
    "mime_type": "audio/mpeg"
  }'

# Get audio file
curl -X GET "http://localhost:5037/api/v1/audio-files/${FILE_ID}" \
  -H "Authorization: Bearer ${TOKEN}"

# Upload file to S3 (using pre-signed URL)
curl -X PUT "${UPLOAD_URL}" \
  -H "Content-Type: audio/mpeg" \
  --data-binary "@test-audio.mp3"
```

## Test Coverage

### Generate Coverage Report

```bash
# Install coverage tool
dotnet tool install --global dotnet-reportgenerator-globaltool

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Generate HTML report
reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"coveragereport" \
  -reporttypes:Html

# Open report
open coveragereport/index.html
```

### Coverage Goals

- **Overall**: > 80%
- **Services**: > 90%
- **Controllers**: > 70%
- **Mappers**: 100%

## Best Practices

### Unit Tests

1. **Naming**: `MethodName_Scenario_ExpectedResult`
   ```csharp
   CreateAudioFileAsync_WithValidData_ReturnsCreatedResponse()
   RegenerateUploadUrlAsync_WithUploadedStatus_ThrowsException()
   ```

2. **AAA Pattern**: Arrange, Act, Assert
   ```csharp
   // Arrange - setup
   // Act - call method
   // Assert - verify results
   ```

3. **One Assertion Per Test**: Focus on single behavior
4. **Mock External Dependencies**: Isolate unit under test
5. **Use FluentAssertions**: More readable assertions

### Integration Tests

1. **Use Test Database**: In-memory or test PostgreSQL instance
2. **Clean State**: Reset database between tests
3. **Test Complete Flows**: Request → Response
4. **Test Error Scenarios**: Invalid data, not found, unauthorized
5. **Verify Side Effects**: Database changes, external calls

### General

1. **Fast Tests**: Unit tests should run in milliseconds
2. **Isolated Tests**: No dependencies between tests
3. **Deterministic**: Same input = same output
4. **Maintainable**: Update tests when code changes
5. **Document Complex Tests**: Add comments for clarity

## Running Tests

### All Tests

```bash
dotnet test VoiceByAuribus-API.sln
```

### Specific Project

```bash
dotnet test VoiceByAuribus.API.Tests/VoiceByAuribus.API.Tests.csproj
dotnet test VoiceByAuribus.AudioUploadNotifier/test/VoiceByAuribus.AudioUploadNotifier.Tests/
```

### Filter by Name

```bash
# Run tests matching pattern
dotnet test --filter "FullyQualifiedName~AudioFileService"

# Run specific test
dotnet test --filter "FullyQualifiedName~CreateAudioFileAsync_WithValidData_ReturnsCreatedResponse"
```

### Parallel Execution

```bash
# Run tests in parallel (default)
dotnet test --parallel

# Run tests sequentially
dotnet test --parallel none
```

### Verbose Output

```bash
dotnet test --verbosity detailed
```

## Continuous Integration

### GitHub Actions Example

```yaml
name: Test

on: [push, pull_request]

jobs:
  test:
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

    steps:
    - uses: actions/checkout@v3

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '10.0.x'

    - name: Restore
      run: dotnet restore

    - name: Build
      run: dotnet build --no-restore

    - name: Test
      run: dotnet test --no-build --verbosity normal --collect:"XPlat Code Coverage"

    - name: Upload Coverage
      uses: codecov/codecov-action@v3
      with:
        files: '**/coverage.cobertura.xml'
```

## Troubleshooting

### Tests Failing Intermittently

- **Cause**: Async timing issues, shared state
- **Solution**: Use proper async/await, isolate tests

### Database Locked

- **Cause**: Connection not closed
- **Solution**: Use `using` statements, dispose contexts

### Mocking Not Working

- **Cause**: Interface not injected, virtual methods
- **Solution**: Ensure interfaces, mark methods virtual

### Integration Tests Slow

- **Cause**: Database operations, external calls
- **Solution**: Use in-memory database, mock external services

---

## Additional Resources

- [xUnit Documentation](https://xunit.net/)
- [Moq Documentation](https://github.com/moq/moq4)
- [FluentAssertions Documentation](https://fluentassertions.com/)
- [Architecture Documentation](architecture.md)
- [Workflows Documentation](workflows.md)
