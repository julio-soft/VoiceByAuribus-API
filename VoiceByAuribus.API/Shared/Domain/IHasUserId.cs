using System;

namespace VoiceByAuribus_API.Shared.Domain;

public interface IHasUserId
{
    Guid? UserId { get; set; }
}
