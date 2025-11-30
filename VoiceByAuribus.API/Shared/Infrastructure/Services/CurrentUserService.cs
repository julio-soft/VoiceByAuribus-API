using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Shared.Infrastructure.Services;

/// <summary>
/// Service for accessing the current authenticated user's information from the HTTP context.
/// </summary>
public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private IReadOnlyCollection<string>? _scopes;

    private HttpContext? HttpContext => httpContextAccessor.HttpContext;
    private ClaimsPrincipal? User => HttpContext?.User;

    /// <summary>
    /// Gets the user ID from the JWT token's 'sub' claim.
    /// For M2M tokens (Cognito client_credentials), this is the client_id.
    /// For user tokens, this is the user's unique identifier.
    /// </summary>
    public string? UserId => GetClaimValue("sub");

    public string? Username =>
        GetClaimValue("preferred_username") ??
        GetClaimValue("username") ??
        GetClaimValue("client_id");

    public string? Email =>
        GetClaimValue(ClaimTypes.Email) ??
        GetClaimValue("email");

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public IReadOnlyCollection<string> Scopes => _scopes ??= ResolveScopes();

    public bool HasScope(string scope) =>
        Scopes.Contains(scope, StringComparer.OrdinalIgnoreCase);

    public bool IsAdmin => HasScope("voice-by-auribus-api/admin");

    private string? GetClaimValue(string claimType)
    {
        return User?.Claims.FirstOrDefault(claim => claim.Type.Equals(claimType, StringComparison.OrdinalIgnoreCase))?.Value;
    }

    private IReadOnlyCollection<string> ResolveScopes()
    {
        if (User is null)
        {
            return Array.Empty<string>();
        }

        var scopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var scopeClaim = GetClaimValue("scope");
        if (!string.IsNullOrWhiteSpace(scopeClaim))
        {
            foreach (var scope in scopeClaim.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                scopes.Add(scope);
            }
        }

        foreach (var groupClaim in User.FindAll("cognito:groups"))
        {
            scopes.Add(groupClaim.Value);
        }

        return scopes.ToArray();
    }
}
