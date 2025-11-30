using System.Collections.Generic;

namespace VoiceByAuribus_API.Features.Auth.Application.Dtos;

public record CurrentUserResponse(
    string? UserId,
    string? Username,
    string? Email,
    IReadOnlyCollection<string> Scopes,
    bool IsAdmin);
