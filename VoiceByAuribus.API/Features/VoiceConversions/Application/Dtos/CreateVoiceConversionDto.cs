using System;
using System.ComponentModel.DataAnnotations;

namespace VoiceByAuribus_API.Features.VoiceConversions.Application.Dtos;

/// <summary>
/// DTO for creating a new voice conversion request.
/// </summary>
public class CreateVoiceConversionDto
{
    /// <summary>
    /// ID of the audio file to convert.
    /// </summary>
    [Required]
    public Guid AudioFileId { get; set; }

    /// <summary>
    /// ID of the voice model to apply.
    /// </summary>
    [Required]
    public Guid VoiceModelId { get; set; }

    /// <summary>
    /// Pitch shift to apply during conversion.
    /// Allowed values: "same_octave", "lower_octave", "higher_octave", "third_down", "third_up", "fifth_down", "fifth_up".
    /// </summary>
    [Required]
    public string PitchShift { get; set; } = string.Empty;

    /// <summary>
    /// Whether to use the preview (short) audio or the full audio for conversion.
    /// Preview conversions are faster but only process a short sample (~10 seconds).
    /// Defaults to false (full audio).
    /// </summary>
    public bool UsePreview { get; set; } = false;
}
