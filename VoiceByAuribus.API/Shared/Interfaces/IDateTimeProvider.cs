using System;

namespace VoiceByAuribus_API.Shared.Interfaces;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
