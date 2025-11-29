using FluentAssertions;
using VoiceByAuribus_API.Features.WebhookSubscriptions.Application.Mappers;
using VoiceByAuribus_API.Features.WebhookSubscriptions.Domain;

namespace VoiceByAuribus_API.Tests.Unit.Features.WebhookSubscriptions.Mappers;

/// <summary>
/// Unit tests for WebhookSubscription mapper extension methods.
/// Tests DTO mapping for subscriptions and delivery logs.
/// </summary>
public class WebhookSubscriptionMappersTests
{
    private readonly DateTime _testDateTime = new(2025, 11, 26, 12, 0, 0, DateTimeKind.Utc);

    #region ToCreatedResponseDto Tests

    /// <summary>
    /// Tests mapping subscription to created response DTO with secret.
    /// </summary>
    [Fact]
    public void ToCreatedResponseDto_WithSubscription_MapsAllFieldsIncludingSecret()
    {
        // Arrange
        const string plainSecret = "test-secret-1234567890123456789012345678901234567890";
        var subscription = new WebhookSubscription
        {
            Id = Guid.NewGuid(),
            Url = "https://example.com/webhook",
            Description = "Test webhook subscription",
            EncryptedSecret = "encrypted-secret-data",
            IsActive = true,
            SubscribedEvents = [WebhookEvent.ConversionCompleted, WebhookEvent.ConversionFailed],
            UserId = Guid.NewGuid(),
            ConsecutiveFailures = 2,
            LastSuccessAt = _testDateTime.AddHours(-2),
            LastFailureAt = _testDateTime.AddHours(-1),
            CreatedAt = _testDateTime.AddDays(-7),
            UpdatedAt = _testDateTime
        };

        // Act
        var result = subscription.ToCreatedResponseDto(plainSecret);

        // Assert
        result.Id.Should().Be(subscription.Id);
        result.Url.Should().Be(subscription.Url);
        result.Description.Should().Be(subscription.Description);
        result.IsActive.Should().BeTrue();
        result.Events.Should().BeEquivalentTo(subscription.SubscribedEvents);
        result.ConsecutiveFailures.Should().Be(2);
        result.LastSuccessAt.Should().Be(subscription.LastSuccessAt);
        result.LastFailureAt.Should().Be(subscription.LastFailureAt);
        result.CreatedAt.Should().Be(subscription.CreatedAt);
        result.UpdatedAt.Should().Be(subscription.UpdatedAt);
        result.Secret.Should().Be(plainSecret, "secret should be included in created response");
    }

    /// <summary>
    /// Tests mapping subscription without optional fields.
    /// </summary>
    [Fact]
    public void ToCreatedResponseDto_WithMinimalData_MapsRequiredFieldsOnly()
    {
        // Arrange
        const string plainSecret = "test-secret-1234567890123456789012345678901234567890";
        var subscription = new WebhookSubscription
        {
            Id = Guid.NewGuid(),
            Url = "https://example.com/webhook",
            EncryptedSecret = "encrypted-secret-data",
            IsActive = true,
            SubscribedEvents = [WebhookEvent.ConversionCompleted],
            UserId = Guid.NewGuid(),
            CreatedAt = _testDateTime,
            UpdatedAt = _testDateTime
        };

        // Act
        var result = subscription.ToCreatedResponseDto(plainSecret);

        // Assert
        result.Id.Should().Be(subscription.Id);
        result.Url.Should().Be(subscription.Url);
        result.Description.Should().BeNull();
        result.IsActive.Should().BeTrue();
        result.ConsecutiveFailures.Should().Be(0);
        result.LastSuccessAt.Should().BeNull();
        result.LastFailureAt.Should().BeNull();
        result.Secret.Should().Be(plainSecret);
    }

    #endregion

    #region ToResponseDto Tests

    /// <summary>
    /// Tests mapping subscription to standard response DTO (without secret).
    /// </summary>
    [Fact]
    public void ToResponseDto_WithSubscription_MapsAllFieldsExceptSecret()
    {
        // Arrange
        var subscription = new WebhookSubscription
        {
            Id = Guid.NewGuid(),
            Url = "https://example.com/webhook",
            Description = "Test webhook subscription",
            EncryptedSecret = "encrypted-secret-data",
            IsActive = true,
            SubscribedEvents = [WebhookEvent.ConversionCompleted],
            UserId = Guid.NewGuid(),
            ConsecutiveFailures = 3,
            LastSuccessAt = _testDateTime.AddHours(-2),
            LastFailureAt = _testDateTime.AddHours(-1),
            CreatedAt = _testDateTime.AddDays(-7),
            UpdatedAt = _testDateTime
        };

        // Act
        var result = subscription.ToResponseDto();

        // Assert
        result.Id.Should().Be(subscription.Id);
        result.Url.Should().Be(subscription.Url);
        result.Description.Should().Be(subscription.Description);
        result.IsActive.Should().BeTrue();
        result.Events.Should().BeEquivalentTo(subscription.SubscribedEvents);
        result.ConsecutiveFailures.Should().Be(3);
        result.LastSuccessAt.Should().Be(subscription.LastSuccessAt);
        result.LastFailureAt.Should().Be(subscription.LastFailureAt);
        result.CreatedAt.Should().Be(subscription.CreatedAt);
        result.UpdatedAt.Should().Be(subscription.UpdatedAt);

        // Verify secret is NOT included in standard response
        var dtoType = result.GetType();
        dtoType.GetProperty("Secret").Should().BeNull("secret should not be exposed in standard response");
    }

    #endregion

    #region ToDeliveryLogDto Tests

    /// <summary>
    /// Tests mapping delivery log to response DTO.
    /// </summary>
    [Fact]
    public void ToDeliveryLogDto_WithDeliveryLog_MapsAllFields()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var deliveryLog = new WebhookDeliveryLog
        {
            Id = Guid.NewGuid(),
            WebhookSubscriptionId = Guid.NewGuid(),
            EventType = "conversion.completed",
            EntityType = "voice_conversion",
            EntityId = entityId,
            Event = WebhookEvent.ConversionCompleted,
            Status = WebhookDeliveryStatus.Delivered,
            AttemptedAt = _testDateTime,
            AttemptNumber = 2,
            HttpStatusCode = 200,
            ResponseBody = "{\"received\":true}",
            ErrorMessage = null,
            DurationMs = 156,
            CreatedAt = _testDateTime.AddMinutes(-5)
        };

        // Act
        var result = deliveryLog.ToDeliveryLogDto();

        // Assert
        result.Id.Should().Be(deliveryLog.Id);
        result.EventType.Should().Be("conversion.completed");
        result.EntityType.Should().Be("voice_conversion");
        result.EntityId.Should().Be(entityId);
        result.Event.Should().Be(WebhookEvent.ConversionCompleted);
        result.Status.Should().Be(WebhookDeliveryStatus.Delivered);
        result.AttemptedAt.Should().Be(_testDateTime);
        result.AttemptNumber.Should().Be(2);
        result.HttpStatusCode.Should().Be(200);
        result.ResponseBody.Should().Be("{\"received\":true}");
        result.ErrorMessage.Should().BeNull();
        result.DurationMs.Should().Be(156);
        result.CreatedAt.Should().Be(deliveryLog.CreatedAt);
    }

    /// <summary>
    /// Tests mapping failed delivery log with error message.
    /// </summary>
    [Fact]
    public void ToDeliveryLogDto_WithFailedDelivery_IncludesErrorMessage()
    {
        // Arrange
        var deliveryLog = new WebhookDeliveryLog
        {
            Id = Guid.NewGuid(),
            WebhookSubscriptionId = Guid.NewGuid(),
            EventType = "conversion.failed",
            EntityType = "voice_conversion",
            EntityId = Guid.NewGuid(),
            Event = WebhookEvent.ConversionFailed,
            Status = WebhookDeliveryStatus.Failed,
            AttemptedAt = _testDateTime,
            AttemptNumber = 3,
            HttpStatusCode = 500,
            ResponseBody = "{\"error\":\"Internal Server Error\"}",
            ErrorMessage = "HTTP 500: Internal Server Error",
            DurationMs = 2034,
            CreatedAt = _testDateTime.AddMinutes(-10)
        };

        // Act
        var result = deliveryLog.ToDeliveryLogDto();

        // Assert
        result.Status.Should().Be(WebhookDeliveryStatus.Failed);
        result.ErrorMessage.Should().Be("HTTP 500: Internal Server Error");
        result.HttpStatusCode.Should().Be(500);
        result.ResponseBody.Should().Contain("Internal Server Error");
    }

    /// <summary>
    /// Tests mapping delivery log with null DurationMs defaults to 0.
    /// </summary>
    [Fact]
    public void ToDeliveryLogDto_WithNullDurationMs_DefaultsToZero()
    {
        // Arrange
        var deliveryLog = new WebhookDeliveryLog
        {
            Id = Guid.NewGuid(),
            WebhookSubscriptionId = Guid.NewGuid(),
            EventType = "webhook.test",
            EntityType = "test",
            Event = WebhookEvent.ConversionCompleted,
            Status = WebhookDeliveryStatus.Pending,
            AttemptNumber = 1,
            DurationMs = null, // Not set yet
            CreatedAt = _testDateTime
        };

        // Act
        var result = deliveryLog.ToDeliveryLogDto();

        // Assert
        result.DurationMs.Should().Be(0, "null DurationMs should default to 0");
    }

    #endregion
}
