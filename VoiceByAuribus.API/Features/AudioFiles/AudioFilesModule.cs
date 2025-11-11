using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using VoiceByAuribus_API.Features.AudioFiles.Application.Dtos;
using VoiceByAuribus_API.Features.AudioFiles.Application.Services;
using VoiceByAuribus_API.Features.AudioFiles.Application.Validators;

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

        // Validators
        services.AddScoped<IValidator<CreateAudioFileDto>, CreateAudioFileValidator>();

        return services;
    }
}
