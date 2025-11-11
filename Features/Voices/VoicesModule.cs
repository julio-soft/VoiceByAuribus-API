using Microsoft.Extensions.DependencyInjection;
using VoiceByAuribus_API.Features.Voices.Application.Services;

namespace VoiceByAuribus_API.Features.Voices;

/// <summary>
/// Dependency injection configuration for the Voices feature.
/// </summary>
public static class VoicesModule
{
    public static IServiceCollection AddVoicesFeature(this IServiceCollection services)
    {
        services.AddScoped<IVoiceModelService, VoiceModelService>();
        
        return services;
    }
}
