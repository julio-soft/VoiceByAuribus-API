using System.Collections.Generic;

namespace VoiceByAuribus_API.Shared.Interfaces;

/// <summary>
/// Service for accessing the current authenticated user's information.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Gets the user ID from the JWT token.
    /// For M2M tokens, this is the client_id. For user tokens, this is the sub claim.
    /// </summary>
    string? UserId { get; }
    string? Username { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    IReadOnlyCollection<string> Scopes { get; }
    bool HasScope(string scope);
    bool IsAdmin { get; }
}
