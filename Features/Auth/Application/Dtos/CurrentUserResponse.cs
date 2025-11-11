using System;
using System.Collections.Generic;

namespace VoiceByAuribus_API.Features.Auth.Application.Dtos;

public record CurrentUserResponse(
    Guid? UserId,
    string? Username,
    string? Email,
    IReadOnlyCollection<string> Scopes,
    bool IsAdmin);
