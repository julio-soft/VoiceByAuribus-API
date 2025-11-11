using System;
using System.Collections.Generic;

namespace VoiceByAuribus_API.Features.Auth.Application.Dtos;

public record AuthStatusResponse(
    bool IsAuthenticated,
    Guid? UserId,
    bool IsAdmin,
    IReadOnlyCollection<string> Scopes);
