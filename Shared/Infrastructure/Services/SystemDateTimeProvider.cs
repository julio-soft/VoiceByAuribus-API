using System;
using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Shared.Infrastructure.Services;

public class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
