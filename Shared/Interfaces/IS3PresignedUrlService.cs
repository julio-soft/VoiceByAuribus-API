using System;

namespace VoiceByAuribus_API.Shared.Interfaces;

public interface IS3PresignedUrlService
{
    string CreatePublicUrl(string bucketName, string key, TimeSpan lifetime);
}
