using System.Threading.Tasks;

namespace VoiceByAuribus_API.Shared.Interfaces;

/// <summary>
/// Service for sending messages to AWS SQS queues.
/// </summary>
public interface ISqsService
{
    /// <summary>
    /// Sends a message to an SQS queue.
    /// </summary>
    /// <typeparam name="T">Type of the message payload</typeparam>
    /// <param name="queueUrl">Full URL of the SQS queue</param>
    /// <param name="message">Message object to serialize and send</param>
    Task SendMessageAsync<T>(string queueUrl, T message);

    /// <summary>
    /// Sends a message to an SQS queue with deduplication ID.
    /// For FIFO queues, prevents duplicate messages within the deduplication interval (5 minutes).
    /// For standard queues, deduplication ID is ignored by SQS but can be used for client-side tracking.
    /// </summary>
    /// <typeparam name="T">Type of the message payload</typeparam>
    /// <param name="queueUrl">Full URL of the SQS queue</param>
    /// <param name="message">Message object to serialize and send</param>
    /// <param name="deduplicationId">Unique ID to prevent duplicate messages (max 128 chars, alphanumeric and punctuation)</param>
    /// <param name="messageGroupId">Message group ID for FIFO queues (required for FIFO queues)</param>
    Task SendMessageAsync<T>(string queueUrl, T message, string deduplicationId, string? messageGroupId = null);
}
