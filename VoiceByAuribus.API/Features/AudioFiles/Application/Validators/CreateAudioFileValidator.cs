using FluentValidation;
using Microsoft.Extensions.Configuration;
using VoiceByAuribus_API.Features.AudioFiles.Application.Dtos;

namespace VoiceByAuribus_API.Features.AudioFiles.Application.Validators;

/// <summary>
/// Validator for CreateAudioFileDto.
/// </summary>
public class CreateAudioFileValidator : AbstractValidator<CreateAudioFileDto>
{
    public CreateAudioFileValidator(IConfiguration configuration)
    {
        var maxFileSizeMB = configuration.GetValue<int>("AWS:S3:MaxFileSizeMB");
        var maxFileSizeBytes = maxFileSizeMB * 1024 * 1024;

        RuleFor(x => x.FileName)
            .NotEmpty()
            .WithMessage("File name is required")
            .MaximumLength(255)
            .WithMessage("File name must not exceed 255 characters");

        RuleFor(x => x.FileSize)
            .GreaterThan(0)
            .WithMessage("File size must be greater than 0")
            .LessThanOrEqualTo(maxFileSizeBytes)
            .WithMessage($"File size must not exceed {maxFileSizeMB}MB");

        RuleFor(x => x.MimeType)
            .NotEmpty()
            .WithMessage("MIME type is required")
            .Must(mimeType => mimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Only audio files are allowed (MIME type must start with 'audio/')");
    }
}
