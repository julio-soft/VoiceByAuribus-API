using System.Text.Json;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Shared.Infrastructure.Services;

/// <summary>
/// Service for sending messages to AWS SQS queues.
/// </summary>
public class SqsService(IAmazonSQS sqsClient) : ISqsService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task SendMessageAsync<T>(string queueUrl, T message)
    {
        var messageBody = JsonSerializer.Serialize(message, _jsonOptions);

        var request = new SendMessageRequest
        {
            QueueUrl = queueUrl,
            MessageBody = messageBody
        };

        await sqsClient.SendMessageAsync(request);
    }

    public async Task SendMessageAsync<T>(string queueUrl, T message, string deduplicationId, string? messageGroupId = null)
    {
        var messageBody = JsonSerializer.Serialize(message, _jsonOptions);

        var request = new SendMessageRequest
        {
            QueueUrl = queueUrl,
            MessageBody = messageBody
        };

        // Add deduplication ID (for FIFO queues, prevents duplicates within 5-minute window)
        if (!string.IsNullOrWhiteSpace(deduplicationId))
        {
            request.MessageDeduplicationId = deduplicationId;
        }

        // Add message group ID (required for FIFO queues)
        if (!string.IsNullOrWhiteSpace(messageGroupId))
        {
            request.MessageGroupId = messageGroupId;
        }

        await sqsClient.SendMessageAsync(request);
    }
}
