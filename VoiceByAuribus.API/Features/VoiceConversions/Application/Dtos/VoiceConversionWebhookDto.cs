using System.ComponentModel.DataAnnotations;

namespace VoiceByAuribus_API.Features.VoiceConversions.Application.Dtos;

/// <summary>
/// DTO for webhook callback from external voice conversion service.
/// </summary>
public class VoiceConversionWebhookDto
{
    /// <summary>
    /// Processing result status.
    /// Valid values: "SUCCESS" or "FAILED"
    /// </summary>
    [Required]
    public required string Status { get; set; }

    /// <summary>
    /// Request ID that identifies the conversion request (echoes the original request_id).
    /// This is the conversion GUID as a string.
    /// </summary>
    [Required]
    public required string RequestId { get; set; }

    /// <summary>
    /// ISO 8601 UTC timestamp when the processing finished.
    /// </summary>
    [Required]
    public required string FinishedAtUtc { get; set; }
}
