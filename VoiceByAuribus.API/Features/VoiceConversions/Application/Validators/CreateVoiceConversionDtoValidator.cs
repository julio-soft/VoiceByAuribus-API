using System.Linq;
using FluentValidation;
using VoiceByAuribus_API.Features.VoiceConversions.Application.Dtos;
using VoiceByAuribus_API.Features.VoiceConversions.Application.Helpers;

namespace VoiceByAuribus_API.Features.VoiceConversions.Application.Validators;

/// <summary>
/// FluentValidation validator for CreateVoiceConversionDto.
/// Validates that PitchShift is one of the allowed values.
/// </summary>
public class CreateVoiceConversionDtoValidator : AbstractValidator<CreateVoiceConversionDto>
{
    public CreateVoiceConversionDtoValidator()
    {
        RuleFor(x => x.AudioFileId)
            .NotEmpty()
            .WithMessage("AudioFileId is required");

        RuleFor(x => x.VoiceModelId)
            .NotEmpty()
            .WithMessage("VoiceModelId is required");

        RuleFor(x => x.PitchShift)
            .NotEmpty()
            .WithMessage("PitchShift is required")
            .Must(BeValidPitchShift)
            .WithMessage(dto => 
            {
                var validValues = string.Join(", ", PitchShiftHelper.GetValidPitchShifts());
                return $"PitchShift must be one of: {validValues}. Provided value: '{dto.PitchShift}'";
            });
    }

    private static bool BeValidPitchShift(string pitchShift)
    {
        if (string.IsNullOrWhiteSpace(pitchShift))
            return false;

        var validPitchShifts = PitchShiftHelper.GetValidPitchShifts();
        return validPitchShifts.Contains(pitchShift, System.StringComparer.OrdinalIgnoreCase);
    }
}
