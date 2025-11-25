namespace VoiceByAuribus_API.Shared.Domain;

/// <summary>
/// Standard event type constants for webhook delivery logs.
/// Uses dot notation (e.g., "conversion.completed") for consistency with webhook conventions.
/// </summary>
public static class WebhookEventTypes
{
    /// <summary>
    /// Event type for successful voice conversion completion.
    /// </summary>
    public const string ConversionCompleted = "conversion.completed";

    /// <summary>
    /// Event type for voice conversion failure.
    /// </summary>
    public const string ConversionFailed = "conversion.failed";

    /// <summary>
    /// Event type for webhook endpoint testing.
    /// </summary>
    public const string Test = "webhook.test";

    // Future extensibility examples:
    // public const string TrainingCompleted = "training.completed";
    // public const string TrainingFailed = "training.failed";
    // public const string ModelDeleted = "model.deleted";
}

/// <summary>
/// Standard entity type constants for webhook delivery logs.
/// Uses snake_case for consistency with API conventions.
/// </summary>
public static class WebhookEntityTypes
{
    /// <summary>
    /// Entity type for voice conversion events.
    /// </summary>
    public const string VoiceConversion = "voice_conversion";

    /// <summary>
    /// Entity type for webhook test events.
    /// </summary>
    public const string Test = "test";

    // Future extensibility examples:
    // public const string VoiceModelTraining = "voice_model_training";
    // public const string AudioFile = "audio_file";
    // public const string VoiceModel = "voice_model";
}
