namespace VoiceByAuribus_API.Features.WebhookSubscriptions.Domain;

/// <summary>
/// Represents the types of events that can trigger webhook notifications.
/// </summary>
public enum WebhookEvent
{
    /// <summary>
    /// Triggered when a voice conversion completes successfully.
    /// </summary>
    ConversionCompleted,

    /// <summary>
    /// Triggered when a voice conversion fails.
    /// </summary>
    ConversionFailed
}
