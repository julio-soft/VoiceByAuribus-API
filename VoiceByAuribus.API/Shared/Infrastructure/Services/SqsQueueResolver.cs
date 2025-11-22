using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;

namespace VoiceByAuribus_API.Shared.Infrastructure.Services;

/// <summary>
/// Resolves SQS queue names to URLs with caching to avoid repeated AWS API calls.
/// </summary>
public class SqsQueueResolver
{
    private readonly IAmazonSQS _sqsClient;
    private readonly ILogger<SqsQueueResolver> _logger;
    private readonly ConcurrentDictionary<string, string> _queueUrlCache = new();

    public SqsQueueResolver(IAmazonSQS sqsClient, ILogger<SqsQueueResolver> logger)
    {
        _sqsClient = sqsClient;
        _logger = logger;
    }

    /// <summary>
    /// Gets the queue URL from the queue name. Results are cached.
    /// </summary>
    /// <param name="queueName">Name of the SQS queue</param>
    /// <returns>Full URL of the queue</returns>
    /// <exception cref="InvalidOperationException">If queue is not found</exception>
    public async Task<string> GetQueueUrlAsync(string queueName)
    {
        // Return from cache if available
        if (_queueUrlCache.TryGetValue(queueName, out var cachedUrl))
        {
            return cachedUrl;
        }

        try
        {
            _logger.LogInformation("Resolving SQS queue URL for queue: {QueueName}", queueName);

            var response = await _sqsClient.GetQueueUrlAsync(new GetQueueUrlRequest
            {
                QueueName = queueName
            });

            var queueUrl = response.QueueUrl;

            // Cache the result
            _queueUrlCache.TryAdd(queueName, queueUrl);

            _logger.LogInformation("SQS queue URL resolved: {QueueName} -> {QueueUrl}", queueName, queueUrl);

            return queueUrl;
        }
        catch (QueueDoesNotExistException ex)
        {
            _logger.LogError(ex, "SQS queue not found: {QueueName}", queueName);
            throw new InvalidOperationException($"SQS queue not found: {queueName}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve SQS queue URL: {QueueName}", queueName);
            throw new InvalidOperationException($"Failed to resolve SQS queue URL: {queueName}", ex);
        }
    }

    /// <summary>
    /// Clears the queue URL cache. Useful for testing or if queue URLs change.
    /// </summary>
    public void ClearCache()
    {
        _queueUrlCache.Clear();
        _logger.LogInformation("SQS queue URL cache cleared");
    }
}
