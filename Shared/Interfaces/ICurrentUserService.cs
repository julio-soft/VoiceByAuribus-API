using System;
using System.Collections.Generic;

namespace VoiceByAuribus_API.Shared.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Username { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    IReadOnlyCollection<string> Scopes { get; }
    bool HasScope(string scope);
    bool IsAdmin { get; }
}
