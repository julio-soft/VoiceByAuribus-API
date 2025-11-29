using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using VoiceByAuribus_API.Shared.Domain;
using VoiceByAuribus_API.Shared.Infrastructure.Middleware;

namespace VoiceByAuribus_API.Tests.Unit.Shared.Middleware;

/// <summary>
/// Unit tests for GlobalExceptionHandlerMiddleware.
/// Tests exception handling and conversion to standardized API responses.
/// </summary>
public class GlobalExceptionHandlerMiddlewareTests
{
    private readonly Mock<ILogger<GlobalExceptionHandlerMiddleware>> _mockLogger;
    private readonly JsonSerializerOptions _jsonOptions;

    public GlobalExceptionHandlerMiddlewareTests()
    {
        _mockLogger = new Mock<ILogger<GlobalExceptionHandlerMiddleware>>();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
    }

    #region Success Path Tests

    /// <summary>
    /// Tests that middleware passes request through when no exception occurs.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithNoException_PassesThroughSuccessfully()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var nextCalled = false;

        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new GlobalExceptionHandlerMiddleware(next, _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue("middleware should pass request to next delegate");
        context.Response.StatusCode.Should().Be(200, "default status code should remain");
    }

    #endregion

    #region Exception Handling Tests

    /// <summary>
    /// Tests that UnauthorizedAccessException returns 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithUnauthorizedAccessException_Returns401()
    {
        // Arrange
        var context = CreateHttpContext();
        const string errorMessage = "User is not authorized to access this resource";

        RequestDelegate next = (ctx) => throw new UnauthorizedAccessException(errorMessage);

        var middleware = new GlobalExceptionHandlerMiddleware(next, _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(401);
        var response = await GetResponseBodyAsync<ApiResponse<object>>(context);
        response.Should().NotBeNull();
        response!.Success.Should().BeFalse();
        response.Message.Should().Be(errorMessage);
        response.Errors.Should().Contain("Unauthorized");
    }

    /// <summary>
    /// Tests that ArgumentException returns 400 Bad Request.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithArgumentException_Returns400()
    {
        // Arrange
        var context = CreateHttpContext();
        const string errorMessage = "Invalid argument provided";

        RequestDelegate next = (ctx) => throw new ArgumentException(errorMessage);

        var middleware = new GlobalExceptionHandlerMiddleware(next, _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(400);
        var response = await GetResponseBodyAsync<ApiResponse<object>>(context);
        response.Should().NotBeNull();
        response!.Success.Should().BeFalse();
        response.Message.Should().Be(errorMessage);
        response.Errors.Should().Contain("Bad Request");
    }

    /// <summary>
    /// Tests that ArgumentNullException returns 400 Bad Request.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithArgumentNullException_Returns400()
    {
        // Arrange
        var context = CreateHttpContext();
        const string paramName = "userId";

        RequestDelegate next = (ctx) => throw new ArgumentNullException(paramName, "User ID cannot be null");

        var middleware = new GlobalExceptionHandlerMiddleware(next, _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(400);
        var response = await GetResponseBodyAsync<ApiResponse<object>>(context);
        response.Should().NotBeNull();
        response!.Success.Should().BeFalse();
        response.Errors.Should().Contain("Bad Request");
    }

    /// <summary>
    /// Tests that KeyNotFoundException returns 404 Not Found.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithKeyNotFoundException_Returns404()
    {
        // Arrange
        var context = CreateHttpContext();
        const string errorMessage = "Resource not found";

        RequestDelegate next = (ctx) => throw new KeyNotFoundException(errorMessage);

        var middleware = new GlobalExceptionHandlerMiddleware(next, _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(404);
        var response = await GetResponseBodyAsync<ApiResponse<object>>(context);
        response.Should().NotBeNull();
        response!.Success.Should().BeFalse();
        response.Message.Should().Be(errorMessage);
        response.Errors.Should().Contain("Not Found");
    }

    /// <summary>
    /// Tests that unhandled exceptions return 500 Internal Server Error.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithUnhandledException_Returns500()
    {
        // Arrange
        var context = CreateHttpContext();

        RequestDelegate next = (ctx) => throw new InvalidOperationException("Unexpected error");

        var middleware = new GlobalExceptionHandlerMiddleware(next, _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(500);
        var response = await GetResponseBodyAsync<ApiResponse<object>>(context);
        response.Should().NotBeNull();
        response!.Success.Should().BeFalse();
        response.Message.Should().Contain("unexpected error occurred");
        response.Message.Should().Contain("trace ID");
        response.Errors.Should().Contain("Internal Server Error");
    }

    #endregion

    #region Response Format Tests

    /// <summary>
    /// Tests that all error responses use application/json content type.
    /// </summary>
    [Theory]
    [InlineData(typeof(UnauthorizedAccessException))]
    [InlineData(typeof(ArgumentException))]
    [InlineData(typeof(KeyNotFoundException))]
    [InlineData(typeof(InvalidOperationException))]
    public async Task InvokeAsync_WithAnyException_ReturnsJsonContentType(Type exceptionType)
    {
        // Arrange
        var context = CreateHttpContext();
        var exception = (Exception)Activator.CreateInstance(exceptionType, "Test error")!;

        RequestDelegate next = (ctx) => throw exception;

        var middleware = new GlobalExceptionHandlerMiddleware(next, _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.ContentType.Should().Be("application/json");
    }

    /// <summary>
    /// Tests that error responses include TraceId in response data.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithException_IncludesTraceIdInResponse()
    {
        // Arrange
        var context = CreateHttpContext();
        const string expectedTraceId = "test-trace-id-12345";
        context.TraceIdentifier = expectedTraceId;

        RequestDelegate next = (ctx) => throw new InvalidOperationException("Test error");

        var middleware = new GlobalExceptionHandlerMiddleware(next, _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var response = await GetResponseBodyAsync<ApiResponse<object>>(context);
        response.Should().NotBeNull();
        response!.Data.Should().NotBeNull();

        var dataElement = (JsonElement)response.Data!;
        dataElement.GetProperty("trace_id").GetString().Should().Be(expectedTraceId);
    }

    #endregion

    #region Logging Tests

    /// <summary>
    /// Tests that middleware logs exceptions with structured context.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithException_LogsErrorWithContext()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Request.Path = "/api/v1/test";
        context.Request.Method = "POST";
        var exception = new InvalidOperationException("Test error");

        RequestDelegate next = (ctx) => throw exception;

        var middleware = new GlobalExceptionHandlerMiddleware(next, _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert - Verify logging occurred (at least once with LogError)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a configured HttpContext for testing with response body capture.
    /// </summary>
    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.TraceIdentifier = "test-trace-id";
        return context;
    }

    /// <summary>
    /// Reads and deserializes the response body from HttpContext.
    /// </summary>
    private async Task<T?> GetResponseBodyAsync<T>(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<T>(body, _jsonOptions);
    }

    #endregion
}
