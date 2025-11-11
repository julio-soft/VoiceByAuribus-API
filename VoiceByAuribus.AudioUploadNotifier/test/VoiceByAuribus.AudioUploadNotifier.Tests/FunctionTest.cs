using System.Net;
using System.Text;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.Lambda.TestUtilities;
using Moq;
using Moq.Protected;
using Xunit;

namespace VoiceByAuribus.AudioUploadNotifier.Tests;

public class FunctionTest
{
    [Fact]
    public async Task TestS3EventLambdaFunction_SuccessfulNotification()
    {
        // Setup mock HTTP client that returns success
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"success\":true}", Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var apiBaseUrl = "https://api.test.com";
        var webhookApiKey = "test-api-key";

        // Setup the S3 event object that S3 notifications would create
        var s3Event = new S3Event
        {
            Records = new List<S3Event.S3EventNotificationRecord>
            {
                new S3Event.S3EventNotificationRecord
                {
                    S3 = new S3Event.S3Entity
                    {
                        Bucket = new S3Event.S3BucketEntity { Name = "test-bucket" },
                        Object = new S3Event.S3ObjectEntity { Key = "audio-files/user123/temp/file123.mp3" }
                    }
                }
            }
        };

        // Invoke the lambda function
        var testLambdaContext = new TestLambdaContext
        {
            Logger = new TestLambdaLogger()
        };

        var function = new Function(httpClient, apiBaseUrl, webhookApiKey);
        await function.FunctionHandler(s3Event, testLambdaContext);

        // Verify that the HTTP request was made
        mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.ToString() == $"{apiBaseUrl}/api/v1/audio-files/webhook/upload-notification" &&
                req.Headers.Contains("X-Webhook-Api-Key")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task TestS3EventLambdaFunction_MultipleRecords()
    {
        // Setup mock HTTP client
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"success\":true}", Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);

        // Setup S3 event with multiple records
        var s3Event = new S3Event
        {
            Records = new List<S3Event.S3EventNotificationRecord>
            {
                new S3Event.S3EventNotificationRecord
                {
                    S3 = new S3Event.S3Entity
                    {
                        Bucket = new S3Event.S3BucketEntity { Name = "test-bucket" },
                        Object = new S3Event.S3ObjectEntity { Key = "file1.mp3" }
                    }
                },
                new S3Event.S3EventNotificationRecord
                {
                    S3 = new S3Event.S3Entity
                    {
                        Bucket = new S3Event.S3BucketEntity { Name = "test-bucket" },
                        Object = new S3Event.S3ObjectEntity { Key = "file2.mp3" }
                    }
                }
            }
        };

        var testLambdaContext = new TestLambdaContext
        {
            Logger = new TestLambdaLogger()
        };

        var function = new Function(httpClient, "https://api.test.com", "test-key");
        await function.FunctionHandler(s3Event, testLambdaContext);

        // Verify that the HTTP request was made twice
        mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task TestS3EventLambdaFunction_ApiErrorThrowsException()
    {
        // Setup mock HTTP client that returns error
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("Internal Server Error", Encoding.UTF8, "text/plain")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);

        var s3Event = new S3Event
        {
            Records = new List<S3Event.S3EventNotificationRecord>
            {
                new S3Event.S3EventNotificationRecord
                {
                    S3 = new S3Event.S3Entity
                    {
                        Bucket = new S3Event.S3BucketEntity { Name = "test-bucket" },
                        Object = new S3Event.S3ObjectEntity { Key = "file.mp3" }
                    }
                }
            }
        };

        var testLambdaContext = new TestLambdaContext
        {
            Logger = new TestLambdaLogger()
        };

        var function = new Function(httpClient, "https://api.test.com", "test-key");

        // Should throw exception when API returns error
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await function.FunctionHandler(s3Event, testLambdaContext));
    }
}