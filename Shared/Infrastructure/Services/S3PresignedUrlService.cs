using System;
using Amazon.S3;
using Amazon.S3.Model;
using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Shared.Infrastructure.Services;

public class S3PresignedUrlService(IAmazonS3 s3Client, IDateTimeProvider dateTimeProvider) : IS3PresignedUrlService
{
    public string CreatePublicUrl(string bucketName, string key, TimeSpan lifetime)
    {
        var expiration = dateTimeProvider.UtcNow.Add(lifetime);

        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucketName,
            Key = key,
            Expires = expiration,
            Verb = HttpVerb.GET
        };

        return s3Client.GetPreSignedURL(request);
    }
}
