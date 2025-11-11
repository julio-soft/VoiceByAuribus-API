using System;
using System.Collections.Generic;

namespace VoiceByAuribus_API.Shared.Interfaces;

public interface IS3PresignedUrlService
{
    string CreatePublicUrl(string bucketName, string key, TimeSpan lifetime);
    string CreateUploadUrl(string bucketName, string key, TimeSpan lifetime, long maxFileSizeBytes, string contentType);
}
