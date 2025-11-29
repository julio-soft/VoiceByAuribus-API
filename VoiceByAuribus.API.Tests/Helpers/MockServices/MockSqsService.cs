using System.Text.Json;
using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Tests.Helpers.MockServices;

/// <summary>
/// Mock implementation of ISqsService for testing.
/// Avoids making real AWS SQS calls during tests.
/// </summary>
public class MockSqsService : ISqsService
{
    private readonly List<SentMessage> _sentMessages = new();

    public Task SendMessageAsync<T>(string queueUrl, T message)
    {
        var messageBody = JsonSerializer.Serialize(message);
        var messageId = Guid.NewGuid().ToString();
        
        _sentMessages.Add(new SentMessage
        {
            MessageId = messageId,
            QueueUrl = queueUrl,
            MessageBody = messageBody,
            SentAt = DateTime.UtcNow
        });

        return Task.CompletedTask;
    }

    public Task SendMessageAsync<T>(
        string queueUrl,
        T message,
        string deduplicationId,
        string? messageGroupId = null)
    {
        var messageBody = JsonSerializer.Serialize(message);
        var messageId = Guid.NewGuid().ToString();
        
        _sentMessages.Add(new SentMessage
        {
            MessageId = messageId,
            QueueUrl = queueUrl,
            MessageBody = messageBody,
            DeduplicationId = deduplicationId,
            MessageGroupId = messageGroupId,
            SentAt = DateTime.UtcNow
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets all messages sent through this mock service.
    /// Useful for verifying that expected messages were sent during tests.
    /// </summary>
    public IReadOnlyList<SentMessage> GetSentMessages() => _sentMessages.AsReadOnly();

    /// <summary>
    /// Clears all sent messages.
    /// Call this between tests for isolation.
    /// </summary>
    public void Clear() => _sentMessages.Clear();

    public class SentMessage
    {
        public string MessageId { get; init; } = string.Empty;
        public string QueueUrl { get; init; } = string.Empty;
        public string MessageBody { get; init; } = string.Empty;
        public string? DeduplicationId { get; init; }
        public string? MessageGroupId { get; init; }
        public DateTime SentAt { get; init; }
    }
}
