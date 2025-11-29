using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Tests.Helpers.MockServices;

/// <summary>
/// Mock implementation of IS3PresignedUrlService for testing.
/// Avoids making real AWS S3 calls during tests.
/// </summary>
public class MockS3PresignedUrlService : IS3PresignedUrlService
{
    public string CreatePublicUrl(string bucketName, string key, TimeSpan lifetime)
    {
        // Return a fake pre-signed URL that looks realistic
        return $"https://{bucketName}.s3.amazonaws.com/{key}?X-Amz-Expires={lifetime.TotalSeconds}";
    }

    public string CreateUploadUrl(
        string bucketName,
        string key,
        TimeSpan lifetime,
        long maxFileSizeBytes,
        string contentType)
    {
        // Return a fake pre-signed PUT URL
        return $"https://{bucketName}.s3.amazonaws.com/{key}?X-Amz-Expires={lifetime.TotalSeconds}&upload=true&max-size={maxFileSizeBytes}";
    }
}
