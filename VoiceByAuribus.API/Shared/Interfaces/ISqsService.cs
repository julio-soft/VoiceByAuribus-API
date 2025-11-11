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
}
