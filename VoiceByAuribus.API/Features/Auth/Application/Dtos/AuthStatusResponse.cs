using System.Collections.Generic;

namespace VoiceByAuribus_API.Features.Auth.Application.Dtos;

public record AuthStatusResponse(
    bool IsAuthenticated,
    string? UserId,
    bool IsAdmin,
    IReadOnlyCollection<string> Scopes);
