using System.ComponentModel.DataAnnotations;

namespace VoiceByAuribus_API.Features.VoiceConversions.Application.Dtos;

/// <summary>
/// DTO for webhook callback from external voice conversion service.
/// </summary>
public class VoiceConversionWebhookDto
{
    /// <summary>
    /// Inference ID that identifies the conversion request.
    /// </summary>
    [Required]
    public Guid InferenceId { get; set; }

    /// <summary>
    /// Status of the conversion: "SUCCESS" or "FAILED".
    /// </summary>
    [Required]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Error message if status is FAILED.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
