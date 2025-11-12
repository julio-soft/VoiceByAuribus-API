using Microsoft.Extensions.DependencyInjection;
using VoiceByAuribus_API.Features.AudioFiles.Application.Services;

namespace VoiceByAuribus_API.Features.AudioFiles;

/// <summary>
/// Dependency injection configuration for the AudioFiles feature.
/// </summary>
public static class AudioFilesModule
{
    public static IServiceCollection AddAudioFilesFeature(this IServiceCollection services)
    {
        // Services
        services.AddScoped<IAudioFileService, AudioFileService>();
        services.AddScoped<IAudioPreprocessingService, AudioPreprocessingService>();

        return services;
    }
}
