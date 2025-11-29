using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using VoiceByAuribus_API.Features.WebhookSubscriptions.Application.Dtos;
using VoiceByAuribus_API.Features.WebhookSubscriptions.Application.Services;
using VoiceByAuribus_API.Features.WebhookSubscriptions.Domain;
using VoiceByAuribus_API.Shared.Infrastructure.Data;
using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Tests.Unit.Features.WebhookSubscriptions.Services;

/// <summary>
/// ⚠️ MOVED TO INTEGRATION TESTS ⚠️
/// WebhookSubscriptionService tests require real PostgreSQL database due to custom
/// EF Core value converters for WebhookEvent[] that don't work with SQLite/InMemory.
/// 
/// These tests will be implemented in Phase 6: Integration Tests with Testcontainers PostgreSQL.
/// See TESTING_STRATEGY.md for details.
/// </summary>
[Trait("Category", "Skipped")]
public class WebhookSubscriptionServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IWebhookSecretService> _mockSecretService;
    private readonly Mock<IWebhookDeliveryService> _mockDeliveryService;
    private readonly Mock<IDateTimeProvider> _mockDateTimeProvider;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<WebhookSubscriptionService>> _mockLogger;
    private readonly WebhookSubscriptionService _service;
    private readonly Guid _testUserId;
    private readonly DateTime _testDateTime;

    public WebhookSubscriptionServiceTests()
    {
        // Setup SQLite in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockDateTimeProvider = new Mock<IDateTimeProvider>();
        _testUserId = Guid.NewGuid();
        _testDateTime = new DateTime(2025, 11, 26, 12, 0, 0, DateTimeKind.Utc);

        _mockCurrentUserService.Setup(x => x.UserId).Returns(_testUserId);
        _mockDateTimeProvider.Setup(x => x.UtcNow).Returns(_testDateTime);

        _context = new ApplicationDbContext(options, _mockCurrentUserService.Object, _mockDateTimeProvider.Object);
        _context.Database.OpenConnection(); // Required for SQLite in-memory
        _context.Database.EnsureCreated();

        _mockSecretService = new Mock<IWebhookSecretService>();
        _mockDeliveryService = new Mock<IWebhookDeliveryService>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<WebhookSubscriptionService>>();

        _mockConfiguration.Setup(x => x.GetSection("Webhooks:Client:MaxSubscriptionsPerUser").Value).Returns("5");

        _service = new WebhookSubscriptionService(
            _context,
            _mockSecretService.Object,
            _mockDeliveryService.Object,
            _mockDateTimeProvider.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _context.Database.CloseConnection(); // Close SQLite connection
        _context.Dispose();
    }

    #region CreateSubscriptionAsync Tests

    /// <summary>
    /// Tests successful creation of a webhook subscription.
    /// </summary>
    [Fact]
    public async Task CreateSubscriptionAsync_WithValidData_CreatesSubscription()
    {
        // Arrange
        const string plainSecret = "test-secret-1234567890123456789012345678901234567890";
        const string encryptedSecret = "encrypted-secret-data";

        _mockSecretService.Setup(x => x.GenerateSecret()).Returns(plainSecret);
        _mockSecretService.Setup(x => x.EncryptSecret(plainSecret)).Returns(encryptedSecret);

        var dto = new CreateWebhookSubscriptionDto
        {
            Url = "https://example.com/webhook",
            Description = "Test webhook",
            Events = [WebhookEvent.ConversionCompleted]
        };

        // Act
        var result = await _service.CreateSubscriptionAsync(dto, _testUserId);

        // Assert
        result.Should().NotBeNull();
        result.Url.Should().Be(dto.Url);
        result.Description.Should().Be(dto.Description);
        result.Events.Should().BeEquivalentTo(dto.Events);
        result.IsActive.Should().BeTrue();
        result.Secret.Should().Be(plainSecret, "plain secret should be returned on creation");

        var savedSubscription = await _context.WebhookSubscriptions.FirstOrDefaultAsync(s => s.Id == result.Id);
        savedSubscription.Should().NotBeNull();
        savedSubscription!.EncryptedSecret.Should().Be(encryptedSecret);
        savedSubscription.UserId.Should().Be(_testUserId);
    }

    /// <summary>
    /// Tests that CreateSubscriptionAsync enforces the maximum subscription limit.
    /// </summary>
    [Fact]
    public async Task CreateSubscriptionAsync_WhenMaxSubscriptionsReached_ThrowsInvalidOperationException()
    {
        // Arrange - Create 5 active subscriptions (the max)
        for (int i = 0; i < 5; i++)
        {
            _context.WebhookSubscriptions.Add(new WebhookSubscription
            {
                Url = $"https://example.com/webhook{i}",
                EncryptedSecret = "encrypted",
                UserId = _testUserId,
                IsActive = true,
                SubscribedEvents = [WebhookEvent.ConversionCompleted]
            });
        }
        await _context.SaveChangesAsync();

        var dto = new CreateWebhookSubscriptionDto
        {
            Url = "https://example.com/webhook-new",
            Events = [WebhookEvent.ConversionCompleted]
        };

        // Act
        var act = async () => await _service.CreateSubscriptionAsync(dto, _testUserId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Maximum number of active subscriptions (5) reached*");
    }

    /// <summary>
    /// Tests that inactive subscriptions don't count toward the limit.
    /// </summary>
    [Fact]
    public async Task CreateSubscriptionAsync_WithInactiveSubscriptions_DoesNotCountTowardLimit()
    {
        // Arrange - Create 5 inactive subscriptions
        for (int i = 0; i < 5; i++)
        {
            _context.WebhookSubscriptions.Add(new WebhookSubscription
            {
                Url = $"https://example.com/webhook{i}",
                EncryptedSecret = "encrypted",
                UserId = _testUserId,
                IsActive = false,
                SubscribedEvents = [WebhookEvent.ConversionCompleted]
            });
        }
        await _context.SaveChangesAsync();

        const string plainSecret = "test-secret-1234567890123456789012345678901234567890";
        _mockSecretService.Setup(x => x.GenerateSecret()).Returns(plainSecret);
        _mockSecretService.Setup(x => x.EncryptSecret(plainSecret)).Returns("encrypted");

        var dto = new CreateWebhookSubscriptionDto
        {
            Url = "https://example.com/webhook-new",
            Events = [WebhookEvent.ConversionCompleted]
        };

        // Act
        var result = await _service.CreateSubscriptionAsync(dto, _testUserId);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region GetUserSubscriptionsAsync Tests

    /// <summary>
    /// Tests retrieval of all user subscriptions ordered by creation date.
    /// </summary>
    [Fact]
    public async Task GetUserSubscriptionsAsync_ReturnsAllUserSubscriptions_OrderedByCreatedAt()
    {
        // Arrange
        var subscription1 = new WebhookSubscription
        {
            Url = "https://example.com/webhook1",
            EncryptedSecret = "encrypted1",
            UserId = _testUserId,
            IsActive = true,
            SubscribedEvents = [WebhookEvent.ConversionCompleted],
            CreatedAt = _testDateTime.AddHours(-2)
        };
        var subscription2 = new WebhookSubscription
        {
            Url = "https://example.com/webhook2",
            EncryptedSecret = "encrypted2",
            UserId = _testUserId,
            IsActive = false,
            SubscribedEvents = [WebhookEvent.ConversionFailed],
            CreatedAt = _testDateTime.AddHours(-1)
        };

        _context.WebhookSubscriptions.AddRange(subscription1, subscription2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetUserSubscriptionsAsync(_testUserId);

        // Assert
        var subscriptions = result.ToList();
        subscriptions.Should().HaveCount(2);
        subscriptions[0].Id.Should().Be(subscription2.Id, "should be ordered by CreatedAt descending");
        subscriptions[1].Id.Should().Be(subscription1.Id);
    }

    /// <summary>
    /// Tests that GetUserSubscriptionsAsync only returns subscriptions for the specified user.
    /// </summary>
    [Fact]
    public async Task GetUserSubscriptionsAsync_OnlyReturnsUserOwnedSubscriptions()
    {
        // Arrange
        var otherUserId = Guid.NewGuid();

        _context.WebhookSubscriptions.AddRange(
            new WebhookSubscription
            {
                Url = "https://example.com/webhook1",
                EncryptedSecret = "encrypted1",
                UserId = _testUserId,
                IsActive = true,
                SubscribedEvents = [WebhookEvent.ConversionCompleted]
            },
            new WebhookSubscription
            {
                Url = "https://example.com/webhook2",
                EncryptedSecret = "encrypted2",
                UserId = otherUserId,
                IsActive = true,
                SubscribedEvents = [WebhookEvent.ConversionCompleted]
            });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetUserSubscriptionsAsync(_testUserId);

        // Assert
        var subscriptions = result.ToList();
        subscriptions.Should().HaveCount(1);
        subscriptions[0].Url.Should().Be("https://example.com/webhook1");
    }

    #endregion

    #region GetSubscriptionByIdAsync Tests

    /// <summary>
    /// Tests retrieval of a specific subscription by ID.
    /// </summary>
    [Fact]
    public async Task GetSubscriptionByIdAsync_WithValidId_ReturnsSubscription()
    {
        // Arrange
        var subscription = new WebhookSubscription
        {
            Url = "https://example.com/webhook",
            EncryptedSecret = "encrypted",
            UserId = _testUserId,
            IsActive = true,
            SubscribedEvents = [WebhookEvent.ConversionCompleted]
        };

        _context.WebhookSubscriptions.Add(subscription);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSubscriptionByIdAsync(subscription.Id, _testUserId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(subscription.Id);
        result.Url.Should().Be(subscription.Url);
    }

    /// <summary>
    /// Tests that GetSubscriptionByIdAsync returns null for non-existent subscription.
    /// </summary>
    [Fact]
    public async Task GetSubscriptionByIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Act
        var result = await _service.GetSubscriptionByIdAsync(Guid.NewGuid(), _testUserId);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// Tests that GetSubscriptionByIdAsync returns null when user doesn't own the subscription.
    /// </summary>
    [Fact]
    public async Task GetSubscriptionByIdAsync_WithDifferentUserId_ReturnsNull()
    {
        // Arrange
        var otherUserId = Guid.NewGuid();
        var subscription = new WebhookSubscription
        {
            Url = "https://example.com/webhook",
            EncryptedSecret = "encrypted",
            UserId = otherUserId,
            IsActive = true,
            SubscribedEvents = [WebhookEvent.ConversionCompleted]
        };

        _context.WebhookSubscriptions.Add(subscription);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSubscriptionByIdAsync(subscription.Id, _testUserId);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region UpdateSubscriptionAsync Tests

    /// <summary>
    /// Tests updating subscription URL.
    /// </summary>
    [Fact]
    public async Task UpdateSubscriptionAsync_WithNewUrl_UpdatesUrl()
    {
        // Arrange
        var subscription = new WebhookSubscription
        {
            Url = "https://example.com/webhook-old",
            EncryptedSecret = "encrypted",
            UserId = _testUserId,
            IsActive = true,
            SubscribedEvents = [WebhookEvent.ConversionCompleted]
        };

        _context.WebhookSubscriptions.Add(subscription);
        await _context.SaveChangesAsync();

        var dto = new UpdateWebhookSubscriptionDto
        {
            Url = "https://example.com/webhook-new"
        };

        // Act
        var result = await _service.UpdateSubscriptionAsync(subscription.Id, dto, _testUserId);

        // Assert
        result.Should().NotBeNull();
        result!.Url.Should().Be("https://example.com/webhook-new");
    }

    /// <summary>
    /// Tests reactivating subscription resets consecutive failures.
    /// </summary>
    [Fact]
    public async Task UpdateSubscriptionAsync_WhenReactivating_ResetsConsecutiveFailures()
    {
        // Arrange
        var subscription = new WebhookSubscription
        {
            Url = "https://example.com/webhook",
            EncryptedSecret = "encrypted",
            UserId = _testUserId,
            IsActive = false,
            ConsecutiveFailures = 5,
            SubscribedEvents = [WebhookEvent.ConversionCompleted]
        };

        _context.WebhookSubscriptions.Add(subscription);
        await _context.SaveChangesAsync();

        var dto = new UpdateWebhookSubscriptionDto
        {
            IsActive = true
        };

        // Act
        var result = await _service.UpdateSubscriptionAsync(subscription.Id, dto, _testUserId);

        // Assert
        result.Should().NotBeNull();
        result!.IsActive.Should().BeTrue();
        result.ConsecutiveFailures.Should().Be(0, "consecutive failures should be reset on reactivation");
    }

    /// <summary>
    /// Tests that UpdateSubscriptionAsync returns null for non-existent subscription.
    /// </summary>
    [Fact]
    public async Task UpdateSubscriptionAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        var dto = new UpdateWebhookSubscriptionDto
        {
            Url = "https://example.com/webhook-new"
        };

        // Act
        var result = await _service.UpdateSubscriptionAsync(Guid.NewGuid(), dto, _testUserId);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region DeleteSubscriptionAsync Tests

    /// <summary>
    /// Tests soft-delete of a subscription.
    /// </summary>
    [Fact]
    public async Task DeleteSubscriptionAsync_WithValidId_SoftDeletesSubscription()
    {
        // Arrange
        var subscription = new WebhookSubscription
        {
            Url = "https://example.com/webhook",
            EncryptedSecret = "encrypted",
            UserId = _testUserId,
            IsActive = true,
            SubscribedEvents = [WebhookEvent.ConversionCompleted]
        };

        _context.WebhookSubscriptions.Add(subscription);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.DeleteSubscriptionAsync(subscription.Id, _testUserId);

        // Assert
        result.Should().BeTrue();

        var deletedSubscription = await _context.WebhookSubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == subscription.Id);

        deletedSubscription.Should().NotBeNull();
        deletedSubscription!.IsDeleted.Should().BeTrue();
    }

    /// <summary>
    /// Tests that DeleteSubscriptionAsync returns false for non-existent subscription.
    /// </summary>
    [Fact]
    public async Task DeleteSubscriptionAsync_WithNonExistentId_ReturnsFalse()
    {
        // Act
        var result = await _service.DeleteSubscriptionAsync(Guid.NewGuid(), _testUserId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region RegenerateSecretAsync Tests

    /// <summary>
    /// Tests secret regeneration returns new secret and resets failure counters.
    /// </summary>
    [Fact]
    public async Task RegenerateSecretAsync_WithValidId_RegeneratesSecretAndResetsFailures()
    {
        // Arrange
        var subscription = new WebhookSubscription
        {
            Url = "https://example.com/webhook",
            EncryptedSecret = "old-encrypted-secret",
            UserId = _testUserId,
            IsActive = true,
            ConsecutiveFailures = 3,
            LastFailureAt = _testDateTime.AddHours(-1),
            SubscribedEvents = [WebhookEvent.ConversionCompleted]
        };

        _context.WebhookSubscriptions.Add(subscription);
        await _context.SaveChangesAsync();

        const string newPlainSecret = "new-secret-1234567890123456789012345678901234567890";
        const string newEncryptedSecret = "new-encrypted-secret";

        _mockSecretService.Setup(x => x.GenerateSecret()).Returns(newPlainSecret);
        _mockSecretService.Setup(x => x.EncryptSecret(newPlainSecret)).Returns(newEncryptedSecret);

        // Act
        var result = await _service.RegenerateSecretAsync(subscription.Id, _testUserId);

        // Assert
        result.Should().NotBeNull();
        result.NewSecret.Should().Be(newPlainSecret);
        result.Message.Should().Contain("ONLY time the new secret will be shown");

        var updatedSubscription = await _context.WebhookSubscriptions.FirstOrDefaultAsync(s => s.Id == subscription.Id);
        updatedSubscription.Should().NotBeNull();
        updatedSubscription!.EncryptedSecret.Should().Be(newEncryptedSecret);
        updatedSubscription.ConsecutiveFailures.Should().Be(0);
        updatedSubscription.LastFailureAt.Should().BeNull();
    }

    /// <summary>
    /// Tests that RegenerateSecretAsync throws for non-existent subscription.
    /// </summary>
    [Fact]
    public async Task RegenerateSecretAsync_WithNonExistentId_ThrowsInvalidOperationException()
    {
        // Act
        var act = async () => await _service.RegenerateSecretAsync(Guid.NewGuid(), _testUserId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Webhook subscription not found");
    }

    #endregion
}
