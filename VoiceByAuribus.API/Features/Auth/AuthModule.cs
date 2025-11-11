using Microsoft.Extensions.DependencyInjection;
using VoiceByAuribus_API.Features.Auth.Application.Services;

namespace VoiceByAuribus_API.Features.Auth;

/// <summary>
/// Dependency injection configuration for the Auth feature.
/// </summary>
public static class AuthModule
{
    public static IServiceCollection AddAuthFeature(this IServiceCollection services)
    {
        services.AddScoped<IAuthReadService, AuthReadService>();
        
        return services;
    }
}
