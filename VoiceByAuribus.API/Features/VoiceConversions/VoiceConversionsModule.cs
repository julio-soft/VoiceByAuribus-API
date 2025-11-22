using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using VoiceByAuribus_API.Features.VoiceConversions.Application.BackgroundServices;
using VoiceByAuribus_API.Features.VoiceConversions.Application.Services;
using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Features.VoiceConversions;

/// <summary>
/// Dependency injection configuration for the VoiceConversions feature.
/// </summary>
public static class VoiceConversionsModule
{
    public static IServiceCollection AddVoiceConversionsFeature(this IServiceCollection services)
    {
        // Services
        services.AddScoped<IVoiceConversionService, VoiceConversionService>();

        // Background Services - Register as singleton to expose health metrics
        services.AddSingleton<VoiceConversionProcessorService>();
        services.AddSingleton<IBackgroundServiceHealthCheck>(sp => sp.GetRequiredService<VoiceConversionProcessorService>());
        services.AddHostedService(sp => sp.GetRequiredService<VoiceConversionProcessorService>());

        // FluentValidation - Auto-registers all validators in this assembly
        // They will automatically integrate with ASP.NET Core ModelState
        services.AddValidatorsFromAssemblyContaining<VoiceConversionService>(ServiceLifetime.Scoped);
        
        // Enable FluentValidation auto-validation (integrates with ModelState)
        services.AddFluentValidationAutoValidation();

        return services;
    }
}
