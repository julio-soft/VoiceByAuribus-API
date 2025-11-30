using Amazon.SQS;
using Amazon.SQS.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using VoiceByAuribus_API.Shared.Infrastructure.Services;

namespace VoiceByAuribus_API.Tests.Unit.Shared.Services;

/// <summary>
/// Unit tests for SqsQueueResolver service.
/// Tests queue name to URL resolution, caching, and error handling.
/// </summary>
public class SqsQueueResolverTests
{
    private readonly Mock<IAmazonSQS> _mockSqsClient;
    private readonly Mock<ILogger<SqsQueueResolver>> _mockLogger;
    private readonly SqsQueueResolver _resolver;

    public SqsQueueResolverTests()
    {
        _mockSqsClient = new Mock<IAmazonSQS>();
        _mockLogger = new Mock<ILogger<SqsQueueResolver>>();
        _resolver = new SqsQueueResolver(_mockSqsClient.Object, _mockLogger.Object);
    }

    #region GetQueueUrlAsync Tests

    /// <summary>
    /// Tests that GetQueueUrlAsync successfully resolves a queue name to URL.
    /// </summary>
    [Fact]
    public async Task GetQueueUrlAsync_WithValidQueueName_ReturnsQueueUrl()
    {
        // Arrange
        const string queueName = "test-queue";
        const string expectedUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/test-queue";

        _mockSqsClient
            .Setup(x => x.GetQueueUrlAsync(It.Is<GetQueueUrlRequest>(r => r.QueueName == queueName), default))
            .ReturnsAsync(new GetQueueUrlResponse { QueueUrl = expectedUrl });

        // Act
        var result = await _resolver.GetQueueUrlAsync(queueName);

        // Assert
        result.Should().Be(expectedUrl);
        _mockSqsClient.Verify(x => x.GetQueueUrlAsync(It.IsAny<GetQueueUrlRequest>(), default), Times.Once);
    }

    /// <summary>
    /// Tests that GetQueueUrlAsync caches results and doesn't make repeated AWS API calls.
    /// </summary>
    [Fact]
    public async Task GetQueueUrlAsync_WithCachedQueue_ReturnsFromCacheWithoutApiCall()
    {
        // Arrange
        const string queueName = "test-queue";
        const string expectedUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/test-queue";

        _mockSqsClient
            .Setup(x => x.GetQueueUrlAsync(It.Is<GetQueueUrlRequest>(r => r.QueueName == queueName), default))
            .ReturnsAsync(new GetQueueUrlResponse { QueueUrl = expectedUrl });

        // Act - First call should hit AWS API
        var result1 = await _resolver.GetQueueUrlAsync(queueName);

        // Act - Second call should return from cache
        var result2 = await _resolver.GetQueueUrlAsync(queueName);

        // Assert
        result1.Should().Be(expectedUrl);
        result2.Should().Be(expectedUrl);
        _mockSqsClient.Verify(x => x.GetQueueUrlAsync(It.IsAny<GetQueueUrlRequest>(), default), Times.Once, 
            "Should only call AWS API once, subsequent calls should use cache");
    }

    /// <summary>
    /// Tests that GetQueueUrlAsync caches different queues independently.
    /// </summary>
    [Fact]
    public async Task GetQueueUrlAsync_WithMultipleQueues_CachesEachSeparately()
    {
        // Arrange
        const string queue1 = "queue-1";
        const string queue2 = "queue-2";
        const string url1 = "https://sqs.us-east-1.amazonaws.com/123456789012/queue-1";
        const string url2 = "https://sqs.us-east-1.amazonaws.com/123456789012/queue-2";

        _mockSqsClient
            .Setup(x => x.GetQueueUrlAsync(It.Is<GetQueueUrlRequest>(r => r.QueueName == queue1), default))
            .ReturnsAsync(new GetQueueUrlResponse { QueueUrl = url1 });

        _mockSqsClient
            .Setup(x => x.GetQueueUrlAsync(It.Is<GetQueueUrlRequest>(r => r.QueueName == queue2), default))
            .ReturnsAsync(new GetQueueUrlResponse { QueueUrl = url2 });

        // Act
        var result1 = await _resolver.GetQueueUrlAsync(queue1);
        var result2 = await _resolver.GetQueueUrlAsync(queue2);
        var result1Cached = await _resolver.GetQueueUrlAsync(queue1);
        var result2Cached = await _resolver.GetQueueUrlAsync(queue2);

        // Assert
        result1.Should().Be(url1);
        result2.Should().Be(url2);
        result1Cached.Should().Be(url1);
        result2Cached.Should().Be(url2);
        
        _mockSqsClient.Verify(x => x.GetQueueUrlAsync(It.Is<GetQueueUrlRequest>(r => r.QueueName == queue1), default), Times.Once);
        _mockSqsClient.Verify(x => x.GetQueueUrlAsync(It.Is<GetQueueUrlRequest>(r => r.QueueName == queue2), default), Times.Once);
    }

    /// <summary>
    /// Tests that GetQueueUrlAsync throws InvalidOperationException when queue doesn't exist.
    /// </summary>
    [Fact]
    public async Task GetQueueUrlAsync_WithNonExistentQueue_ThrowsInvalidOperationException()
    {
        // Arrange
        const string queueName = "non-existent-queue";

        _mockSqsClient
            .Setup(x => x.GetQueueUrlAsync(It.Is<GetQueueUrlRequest>(r => r.QueueName == queueName), default))
            .ThrowsAsync(new QueueDoesNotExistException("Queue does not exist"));

        // Act
        var act = async () => await _resolver.GetQueueUrlAsync(queueName);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"SQS queue not found: {queueName}");
    }

    /// <summary>
    /// Tests that GetQueueUrlAsync wraps general exceptions in InvalidOperationException.
    /// </summary>
    [Fact]
    public async Task GetQueueUrlAsync_WithAwsException_ThrowsInvalidOperationException()
    {
        // Arrange
        const string queueName = "test-queue";

        _mockSqsClient
            .Setup(x => x.GetQueueUrlAsync(It.IsAny<GetQueueUrlRequest>(), default))
            .ThrowsAsync(new AmazonSQSException("AWS service error"));

        // Act
        var act = async () => await _resolver.GetQueueUrlAsync(queueName);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Failed to resolve SQS queue URL: {queueName}");
    }

    #endregion

    #region ClearCache Tests

    /// <summary>
    /// Tests that ClearCache removes all cached queue URLs.
    /// </summary>
    [Fact]
    public async Task ClearCache_RemovesCachedUrls_CausesNewApiCall()
    {
        // Arrange
        const string queueName = "test-queue";
        const string expectedUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/test-queue";

        _mockSqsClient
            .Setup(x => x.GetQueueUrlAsync(It.Is<GetQueueUrlRequest>(r => r.QueueName == queueName), default))
            .ReturnsAsync(new GetQueueUrlResponse { QueueUrl = expectedUrl });

        // Act - First call should hit AWS API
        var result1 = await _resolver.GetQueueUrlAsync(queueName);

        // Verify cached (no new API call)
        var result2 = await _resolver.GetQueueUrlAsync(queueName);
        _mockSqsClient.Verify(x => x.GetQueueUrlAsync(It.IsAny<GetQueueUrlRequest>(), default), Times.Once);

        // Clear cache
        _resolver.ClearCache();

        // After clear, should hit AWS API again
        var result3 = await _resolver.GetQueueUrlAsync(queueName);

        // Assert
        result1.Should().Be(expectedUrl);
        result2.Should().Be(expectedUrl);
        result3.Should().Be(expectedUrl);
        _mockSqsClient.Verify(x => x.GetQueueUrlAsync(It.IsAny<GetQueueUrlRequest>(), default), Times.Exactly(2),
            "Should call AWS API twice: once before cache clear and once after");
    }

    /// <summary>
    /// Tests that ClearCache can be called on empty cache without errors.
    /// </summary>
    [Fact]
    public void ClearCache_OnEmptyCache_DoesNotThrow()
    {
        // Act
        var act = () => _resolver.ClearCache();

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region FIFO Queue Tests

    /// <summary>
    /// Tests that GetQueueUrlAsync works with FIFO queue names (.fifo suffix).
    /// </summary>
    [Fact]
    public async Task GetQueueUrlAsync_WithFifoQueue_ResolvesFifoQueueUrl()
    {
        // Arrange
        const string queueName = "voice-by-auribus-preprocessing.fifo";
        const string expectedUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/voice-by-auribus-preprocessing.fifo";

        _mockSqsClient
            .Setup(x => x.GetQueueUrlAsync(It.Is<GetQueueUrlRequest>(r => r.QueueName == queueName), default))
            .ReturnsAsync(new GetQueueUrlResponse { QueueUrl = expectedUrl });

        // Act
        var result = await _resolver.GetQueueUrlAsync(queueName);

        // Assert
        result.Should().Be(expectedUrl);
        result.Should().EndWith(".fifo");
    }

    #endregion
}
