using System;
using System.Collections.Generic;
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

    public string CreateUploadUrl(string bucketName, string key, TimeSpan lifetime, long maxFileSizeBytes, string contentType)
    {
        var expiration = dateTimeProvider.UtcNow.Add(lifetime);

        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucketName,
            Key = key,
            Expires = expiration,
            Verb = HttpVerb.PUT,
            ContentType = contentType
        };

        // Add metadata for size constraint
        request.Metadata.Add("x-amz-content-length-range", $"0,{maxFileSizeBytes}");

        return s3Client.GetPreSignedURL(request);
    }
}
