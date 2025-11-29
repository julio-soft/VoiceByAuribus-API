using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using VoiceByAuribus_API.Features.WebhookSubscriptions.Application.Services;
using VoiceByAuribus_API.Features.WebhookSubscriptions.Domain;
using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Tests.Unit.Features.WebhookSubscriptions.Services;

/// <summary>
/// Unit tests for WebhookDeliveryService.
/// Tests HTTP delivery, retry logic, HMAC signatures, and error handling.
/// </summary>
public class WebhookDeliveryServiceTests
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<IWebhookSecretService> _mockSecretService;
    private readonly Mock<IDateTimeProvider> _mockDateTimeProvider;
    private readonly Mock<ILogger<WebhookDeliveryService>> _mockLogger;
    private readonly WebhookDeliveryService _service;
    private readonly DateTime _testDateTime;

    public WebhookDeliveryServiceTests()
    {
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockSecretService = new Mock<IWebhookSecretService>();
        _mockDateTimeProvider = new Mock<IDateTimeProvider>();
        _mockLogger = new Mock<ILogger<WebhookDeliveryService>>();

        _testDateTime = new DateTime(2025, 11, 26, 12, 0, 0, DateTimeKind.Utc);
        _mockDateTimeProvider.Setup(x => x.UtcNow).Returns(_testDateTime);

        _service = new WebhookDeliveryService(
            _mockHttpClientFactory.Object,
            _mockSecretService.Object,
            _mockDateTimeProvider.Object,
            _mockLogger.Object);
    }

    #region Successful Delivery Tests

    /// <summary>
    /// Tests successful webhook delivery with 200 OK response.
    /// </summary>
    [Fact]
    public async Task DeliverWebhookAsync_WithSuccessfulResponse_MarksAsDelivered()
    {
        // Arrange
        var subscription = CreateTestSubscription();
        var deliveryLog = CreateTestDeliveryLog(subscription);
        const string secret = "test-secret-1234567890123456789012345678901234567890";
        const string signature = "computed-hmac-signature";

        _mockSecretService.Setup(x => x.ComputeHmacSignature(secret, It.IsAny<string>()))
            .Returns(signature);

        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"received\":true}")
            });

        var httpClient = new HttpClient(httpMessageHandler.Object);
        _mockHttpClientFactory.Setup(x => x.CreateClient("WebhookClient")).Returns(httpClient);

        // Act
        var result = await _service.DeliverWebhookAsync(deliveryLog, secret);

        // Assert
        result.Status.Should().Be(WebhookDeliveryStatus.Delivered);
        result.HttpStatusCode.Should().Be(200);
        result.ResponseBody.Should().Contain("received");
        result.ErrorMessage.Should().BeNull();
        result.AttemptedAt.Should().Be(_testDateTime);
        result.DurationMs.Should().BeGreaterOrEqualTo(0, "duration should be set");
    }

    /// <summary>
    /// Tests that delivery sets correct HTTP headers including HMAC signature.
    /// </summary>
    [Fact]
    public async Task DeliverWebhookAsync_SetsCorrectHeaders()
    {
        // Arrange
        var subscription = CreateTestSubscription();
        var deliveryLog = CreateTestDeliveryLog(subscription);
        const string secret = "test-secret-1234567890123456789012345678901234567890";
        const string signature = "computed-hmac-signature";

        _mockSecretService.Setup(x => x.ComputeHmacSignature(secret, It.IsAny<string>()))
            .Returns(signature);

        HttpRequestMessage? capturedRequest = null;
        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var httpClient = new HttpClient(httpMessageHandler.Object);
        _mockHttpClientFactory.Setup(x => x.CreateClient("WebhookClient")).Returns(httpClient);

        // Act
        await _service.DeliverWebhookAsync(deliveryLog, secret);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.GetValues("X-Webhook-Signature").First().Should().Be($"sha256={signature}");
        capturedRequest.Headers.GetValues("X-Webhook-Id").First().Should().Be(deliveryLog.Id.ToString());
        capturedRequest.Headers.GetValues("X-Webhook-Event").Should().NotBeEmpty();
        capturedRequest.Headers.GetValues("X-Webhook-Timestamp").Should().NotBeEmpty();
        capturedRequest.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    #endregion

    #region Failed Delivery Tests

    /// <summary>
    /// Tests webhook delivery with 4xx client error response.
    /// </summary>
    [Fact]
    public async Task DeliverWebhookAsync_With4xxResponse_MarksAsFailed()
    {
        // Arrange
        var subscription = CreateTestSubscription();
        var deliveryLog = CreateTestDeliveryLog(subscription);
        const string secret = "test-secret-1234567890123456789012345678901234567890";

        _mockSecretService.Setup(x => x.ComputeHmacSignature(secret, It.IsAny<string>()))
            .Returns("signature");

        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                ReasonPhrase = "Bad Request",
                Content = new StringContent("{\"error\":\"Invalid payload\"}")
            });

        var httpClient = new HttpClient(httpMessageHandler.Object);
        _mockHttpClientFactory.Setup(x => x.CreateClient("WebhookClient")).Returns(httpClient);

        // Act
        var result = await _service.DeliverWebhookAsync(deliveryLog, secret);

        // Assert
        result.Status.Should().Be(WebhookDeliveryStatus.Failed);
        result.HttpStatusCode.Should().Be(400);
        result.ErrorMessage.Should().Contain("BadRequest");
        result.ResponseBody.Should().Contain("Invalid payload");
    }

    /// <summary>
    /// Tests webhook delivery with 5xx server error response.
    /// </summary>
    [Fact]
    public async Task DeliverWebhookAsync_With5xxResponse_MarksAsFailed()
    {
        // Arrange
        var subscription = CreateTestSubscription();
        var deliveryLog = CreateTestDeliveryLog(subscription);
        const string secret = "test-secret-1234567890123456789012345678901234567890";

        _mockSecretService.Setup(x => x.ComputeHmacSignature(secret, It.IsAny<string>()))
            .Returns("signature");

        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                ReasonPhrase = "Internal Server Error"
            });

        var httpClient = new HttpClient(httpMessageHandler.Object);
        _mockHttpClientFactory.Setup(x => x.CreateClient("WebhookClient")).Returns(httpClient);

        // Act
        var result = await _service.DeliverWebhookAsync(deliveryLog, secret);

        // Assert
        result.Status.Should().Be(WebhookDeliveryStatus.Failed);
        result.HttpStatusCode.Should().Be(500);
        result.ErrorMessage.Should().Contain("InternalServerError");
    }

    /// <summary>
    /// Tests webhook delivery with network error (HttpRequestException).
    /// </summary>
    [Fact]
    public async Task DeliverWebhookAsync_WithNetworkError_MarksAsFailedWithErrorMessage()
    {
        // Arrange
        var subscription = CreateTestSubscription();
        var deliveryLog = CreateTestDeliveryLog(subscription);
        const string secret = "test-secret-1234567890123456789012345678901234567890";

        _mockSecretService.Setup(x => x.ComputeHmacSignature(secret, It.IsAny<string>()))
            .Returns("signature");

        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network connection failed"));

        var httpClient = new HttpClient(httpMessageHandler.Object);
        _mockHttpClientFactory.Setup(x => x.CreateClient("WebhookClient")).Returns(httpClient);

        // Act
        var result = await _service.DeliverWebhookAsync(deliveryLog, secret);

        // Assert
        result.Status.Should().Be(WebhookDeliveryStatus.Failed);
        result.ErrorMessage.Should().Contain("Network error");
        result.ErrorMessage.Should().Contain("connection failed");
        result.HttpStatusCode.Should().BeNull();
    }

    /// <summary>
    /// Tests webhook delivery with timeout (TaskCanceledException).
    /// </summary>
    [Fact]
    public async Task DeliverWebhookAsync_WithTimeout_MarksAsFailedWithTimeoutError()
    {
        // Arrange
        var subscription = CreateTestSubscription();
        var deliveryLog = CreateTestDeliveryLog(subscription);
        const string secret = "test-secret-1234567890123456789012345678901234567890";

        _mockSecretService.Setup(x => x.ComputeHmacSignature(secret, It.IsAny<string>()))
            .Returns("signature");

        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timed out"));

        var httpClient = new HttpClient(httpMessageHandler.Object);
        _mockHttpClientFactory.Setup(x => x.CreateClient("WebhookClient")).Returns(httpClient);

        // Act
        var result = await _service.DeliverWebhookAsync(deliveryLog, secret);

        // Assert
        result.Status.Should().Be(WebhookDeliveryStatus.Failed);
        result.ErrorMessage.Should().Be("Request timeout");
    }

    /// <summary>
    /// Tests webhook delivery with unexpected exception.
    /// </summary>
    [Fact]
    public async Task DeliverWebhookAsync_WithUnexpectedException_MarksAsFailedWithErrorMessage()
    {
        // Arrange
        var subscription = CreateTestSubscription();
        var deliveryLog = CreateTestDeliveryLog(subscription);
        const string secret = "test-secret-1234567890123456789012345678901234567890";

        _mockSecretService.Setup(x => x.ComputeHmacSignature(secret, It.IsAny<string>()))
            .Returns("signature");

        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Unexpected error occurred"));

        var httpClient = new HttpClient(httpMessageHandler.Object);
        _mockHttpClientFactory.Setup(x => x.CreateClient("WebhookClient")).Returns(httpClient);

        // Act
        var result = await _service.DeliverWebhookAsync(deliveryLog, secret);

        // Assert
        result.Status.Should().Be(WebhookDeliveryStatus.Failed);
        result.ErrorMessage.Should().Contain("Unexpected error");
    }

    #endregion

    #region Response Body Tests

    /// <summary>
    /// Tests that long response bodies are truncated to 2000 characters.
    /// </summary>
    [Fact]
    public async Task DeliverWebhookAsync_WithLongResponseBody_TruncatesTo2000Chars()
    {
        // Arrange
        var subscription = CreateTestSubscription();
        var deliveryLog = CreateTestDeliveryLog(subscription);
        const string secret = "test-secret-1234567890123456789012345678901234567890";

        _mockSecretService.Setup(x => x.ComputeHmacSignature(secret, It.IsAny<string>()))
            .Returns("signature");

        var longResponseBody = new string('x', 3000);
        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(longResponseBody)
            });

        var httpClient = new HttpClient(httpMessageHandler.Object);
        _mockHttpClientFactory.Setup(x => x.CreateClient("WebhookClient")).Returns(httpClient);

        // Act
        var result = await _service.DeliverWebhookAsync(deliveryLog, secret);

        // Assert
        result.ResponseBody.Should().NotBeNull();
        result.ResponseBody!.Length.Should().Be(2000, "response body should be truncated to 2000 characters");
    }

    #endregion

    #region Helper Methods

    private static WebhookSubscription CreateTestSubscription()
    {
        return new WebhookSubscription
        {
            Id = Guid.NewGuid(),
            Url = "https://example.com/webhook",
            EncryptedSecret = "encrypted-secret",
            UserId = Guid.NewGuid(),
            IsActive = true,
            SubscribedEvents = [WebhookEvent.ConversionCompleted]
        };
    }

    private static WebhookDeliveryLog CreateTestDeliveryLog(WebhookSubscription subscription)
    {
        return new WebhookDeliveryLog
        {
            Id = Guid.NewGuid(),
            WebhookSubscriptionId = subscription.Id,
            WebhookSubscription = subscription,
            EventType = "conversion.completed",
            EntityType = "voice_conversion",
            EntityId = Guid.NewGuid(),
            Event = WebhookEvent.ConversionCompleted,
            Status = WebhookDeliveryStatus.Pending,
            PayloadJson = "{\"event\":\"conversion.completed\",\"data\":{\"id\":\"test\"}}",
            AttemptNumber = 1
        };
    }

    #endregion
}
