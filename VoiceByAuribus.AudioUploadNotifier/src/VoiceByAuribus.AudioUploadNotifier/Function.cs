using System.Text;
using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace VoiceByAuribus.AudioUploadNotifier;

/// <summary>
/// Lambda function that handles S3 upload events and notifies the VoiceByAuribus API backend.
/// </summary>
public class Function
{
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;
    private readonly string _webhookApiKey;

    /// <summary>
    /// Default constructor. This constructor is used by Lambda to construct the instance.
    /// Configuration is read from environment variables.
    /// </summary>
    public Function()
    {
        _httpClient = new HttpClient();
        _apiBaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL")
            ?? throw new InvalidOperationException("API_BASE_URL environment variable is required");
        _webhookApiKey = Environment.GetEnvironmentVariable("WEBHOOK_API_KEY")
            ?? throw new InvalidOperationException("WEBHOOK_API_KEY environment variable is required");
    }

    /// <summary>
    /// Constructs an instance with a preconfigured HttpClient. This can be used for testing.
    /// </summary>
    /// <param name="httpClient">The HttpClient to use for API calls.</param>
    /// <param name="apiBaseUrl">The base URL of the API.</param>
    /// <param name="webhookApiKey">The API key for webhook authentication.</param>
    public Function(HttpClient httpClient, string apiBaseUrl, string webhookApiKey)
    {
        _httpClient = httpClient;
        _apiBaseUrl = apiBaseUrl;
        _webhookApiKey = webhookApiKey;
    }

    /// <summary>
    /// This method is called for every Lambda invocation. It processes S3 upload events
    /// and notifies the backend API.
    /// </summary>
    /// <param name="evnt">The S3 event for the Lambda function handler to process.</param>
    /// <param name="context">The ILambdaContext that provides methods for logging and describing the Lambda environment.</param>
    public async Task FunctionHandler(S3Event evnt, ILambdaContext context)
    {
        var eventRecords = evnt.Records ?? new List<S3Event.S3EventNotificationRecord>();

        context.Logger.LogInformation($"Processing {eventRecords.Count} S3 event record(s)");

        foreach (var record in eventRecords)
        {
            var s3Event = record.S3;
            if (s3Event == null)
            {
                context.Logger.LogWarning("Skipping record with null S3 event data");
                continue;
            }

            try
            {
                var bucketName = s3Event.Bucket.Name;
                var objectKey = s3Event.Object.Key;
                var s3Uri = $"s3://{bucketName}/{objectKey}";

                context.Logger.LogInformation($"Processing upload notification for: {s3Uri}");

                await NotifyBackendAsync(s3Uri, context);

                context.Logger.LogInformation($"Successfully notified backend for: {s3Uri}");
            }
            catch (Exception e)
            {
                context.Logger.LogError($"Error processing S3 event for bucket {s3Event.Bucket.Name}, key {s3Event.Object.Key}");
                context.Logger.LogError($"Error: {e.Message}");
                context.Logger.LogError($"Stack trace: {e.StackTrace}");

                // Re-throw to trigger Lambda retry mechanism
                throw;
            }
        }
    }

    /// <summary>
    /// Sends upload notification to the backend API.
    /// </summary>
    private async Task NotifyBackendAsync(string s3Uri, ILambdaContext context)
    {
        var webhookUrl = $"{_apiBaseUrl.TrimEnd('/')}/api/v1/audio-files/webhook/upload-notification";

        var payload = new { s3_uri = s3Uri };
        var jsonContent = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        var request = new HttpRequestMessage(HttpMethod.Post, webhookUrl)
        {
            Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
        };

        request.Headers.Add("X-Webhook-Api-Key", _webhookApiKey);

        context.Logger.LogInformation($"Sending POST request to: {webhookUrl}");

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Backend API returned error status {response.StatusCode}. Response: {responseBody}");
        }

        context.Logger.LogInformation($"Backend API responded with status: {response.StatusCode}");
    }
}