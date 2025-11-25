# Webhook and API Conventions - VoiceByAuribus API

**Date**: 2025-11-24
**Purpose**: Document architectural patterns and conventions to maintain consistency across the API

## Table of Contents
1. [PitchShift Abstraction](#pitchshift-abstraction)
2. [Webhook Event-Agnostic Design](#webhook-event-agnostic-design)
3. [Temporary URL Handling](#temporary-url-handling)
4. [Future Extensibility](#future-extensibility)

---

## PitchShift Abstraction

### Rule: NEVER expose internal `Transposition` enum to external APIs

**Internal Implementation**: `Transposition` enum
```csharp
public enum Transposition
{
    SameOctave = 0,
    LowerOctave = -12,
    HigherOctave = 12,
    ThirdDown = -4,
    ThirdUp = 4,
    FifthDown = -7,
    FifthUp = 7
}
```

**External API**: `pitch_shift` string values
```json
{
  "pitch_shift": "same_octave"  // ✅ Correct
  // NOT "transposition": "0"   // ❌ Wrong - exposes internal implementation
}
```

### Valid PitchShift Values

| pitch_shift string | Description | Internal Transposition |
|-------------------|-------------|----------------------|
| `same_octave` | No pitch change | `Transposition.SameOctave` (0) |
| `lower_octave` | One octave down | `Transposition.LowerOctave` (-12) |
| `higher_octave` | One octave up | `Transposition.HigherOctave` (12) |
| `third_down` | Minor third down | `Transposition.ThirdDown` (-4) |
| `third_up` | Minor third up | `Transposition.ThirdUp` (4) |
| `fifth_down` | Perfect fifth down | `Transposition.FifthDown` (-7) |
| `fifth_up` | Perfect fifth up | `Transposition.FifthUp` (7) |

### Helper Usage

**File**: `Features/VoiceConversions/Application/Helpers/PitchShiftHelper.cs`

```csharp
// ✅ Convert external string to internal enum
var transposition = PitchShiftHelper.ToTransposition("same_octave");

// ✅ Convert internal enum to external string
var pitchShift = PitchShiftHelper.ToPitchShiftString(Transposition.SameOctave);
// Returns: "same_octave"
```

### Where This Applies

1. **Voice Conversion API** (`/api/v1/voice-conversions`)
   - Request: `POST /voice-conversions` with `pitch_shift` field
   - Response: `GET /voice-conversions/{id}` with `pitch_shift` field

2. **Webhook Payloads** (✅ Fixed 2025-11-24)
   - Event payloads include `pitch_shift`, not `transposition`
   - File: `WebhookEventPublisher.cs`
   ```csharp
   conversionData["pitch_shift"] = PitchShiftHelper.ToPitchShiftString(conversion.Transposition);
   ```

3. **Any Future APIs** that expose voice conversion data

---

## Webhook Event-Agnostic Design

### Problem Solved

Original design was tightly coupled to `VoiceConversion` entity:
```csharp
// ❌ Old Design - Feature-specific
public class WebhookDeliveryLog
{
    public Guid? VoiceConversionId { get; set; }
    public VoiceConversion? VoiceConversion { get; set; }  // Hard FK
    public WebhookEvent Event { get; set; }  // Limited to conversion events
}
```

This prevented:
- Adding `VoiceModelTraining` events
- Adding other future event types
- Scaling webhook system beyond voice conversions

### Solution: Generic Event Design (✅ Implemented 2025-11-24)

```csharp
// ✅ New Design - Event-agnostic
public class WebhookDeliveryLog : BaseAuditableEntity
{
    // Generic fields
    public string EventType { get; set; }     // "conversion.completed", "training.completed"
    public string? EntityType { get; set; }   // "voice_conversion", "voice_model_training"
    public Guid? EntityId { get; set; }       // Generic entity ID

    // Backwards compatibility
    public WebhookEvent Event { get; set; }   // Keep enum for filtering

    // Delivery tracking
    public WebhookDeliveryStatus Status { get; set; }
    public string PayloadJson { get; set; }
    // ... other fields
}
```

### Database Schema

**Migration**: `20251124175636_RefactorWebhookDeliveryLogToBeEventAgnostic`

```sql
-- Removed FK to voice_conversions
ALTER TABLE webhook_delivery_logs DROP CONSTRAINT "FK_webhook_delivery_logs_voice_conversions_VoiceConversionId";

-- Renamed column
ALTER TABLE webhook_delivery_logs RENAME COLUMN "VoiceConversionId" TO "EntityId";

-- Added generic fields
ALTER TABLE webhook_delivery_logs ADD "EntityType" VARCHAR(100);
ALTER TABLE webhook_delivery_logs ADD "EventType" VARCHAR(100) NOT NULL;

-- New indexes for performance
CREATE INDEX "IX_webhook_delivery_logs_EntityType" ON webhook_delivery_logs ("EntityType");
CREATE INDEX "IX_webhook_delivery_logs_EventType" ON webhook_delivery_logs ("EventType");
```

### Usage Pattern for Future Events

**⚠️ IMPORTANT: Use WebhookEventTypes and WebhookEntityTypes constants**

All event and entity type strings are standardized in `Shared/Domain/WebhookEventTypes.cs`:

```csharp
// Current constants (in production)
WebhookEventTypes.ConversionCompleted  // "conversion.completed"
WebhookEventTypes.ConversionFailed     // "conversion.failed"
WebhookEventTypes.Test                 // "webhook.test"

WebhookEntityTypes.VoiceConversion     // "voice_conversion"
WebhookEntityTypes.Test                // "test"
```

**Example: Voice Model Training Events (Future Implementation)**

```csharp
// Step 1: Add constants to WebhookEventTypes.cs
public static class WebhookEventTypes
{
    public const string TrainingCompleted = "training.completed";
    public const string TrainingFailed = "training.failed";
}

public static class WebhookEntityTypes
{
    public const string VoiceModelTraining = "voice_model_training";
}

// Step 2: Use constants in VoiceModelTrainingPublisher.cs (future)
var deliveryLog = new WebhookDeliveryLog
{
    WebhookSubscriptionId = subscription.Id,
    EventType = WebhookEventTypes.TrainingCompleted,    // ✅ Type-safe constant
    EntityType = WebhookEntityTypes.VoiceModelTraining, // ✅ Type-safe constant
    EntityId = training.Id,                             // ✅ Training ID (not conversion ID)
    Event = WebhookEvent.TrainingCompleted,             // ✅ New enum value (to be added)
    Status = WebhookDeliveryStatus.Pending,
    PayloadJson = payloadJson
};
```

### Benefits

1. ✅ **Scalable** - Supports any future event type
2. ✅ **No breaking changes** - Can add new events without schema changes
3. ✅ **Queryable** - Can filter by `EntityType` or `EventType`
4. ✅ **Flexible** - Payload contains all necessary data in JSON

---

## Temporary URL Handling

### Rule: NEVER include temporary URLs in webhook payloads

**Problem**: Pre-signed S3 URLs expire (typically 12 hours). If stored in webhook logs/payloads, they become invalid.

### ❌ Old Approach (Wrong)

```json
{
  "event": "conversion.completed",
  "data": {
    "conversion": {
      "id": "...",
      "output_url": "https://bucket.s3.amazonaws.com/...?X-Amz-Expires=43200"
    }
  }
}
```

**Problems**:
- URL expires in 12 hours
- Webhook payload stored in database becomes stale
- Client may store payload and try to use expired URL later

### ✅ Correct Approach (Fixed 2025-11-24)

**Webhook payload** (no `output_url`):
```json
{
  "event": "conversion.completed",
  "data": {
    "conversion": {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "status": "completed",
      // ... other fields
      // ✅ NO output_url field
    }
  }
}
```

**Client workflow**:
```javascript
// 1. Receive webhook
app.post('/webhook', (req, res) => {
    const conversionId = req.body.data.conversion.id;

    // 2. Call API to get fresh URL
    const response = await fetch(`https://api.voicebyauribus.com/api/v1/voice-conversions/${conversionId}`);
    const { output_url } = await response.json();

    // 3. Download file with fresh URL (valid for 12 hours)
    await downloadFile(output_url);
});
```

### Implementation

**File**: `WebhookEventPublisher.cs`

```csharp
// ✅ Removed output_url generation
if (eventType == WebhookEvent.ConversionCompleted)
{
    // Calculate processing duration
    if (conversion.ProcessingStartedAt.HasValue && conversion.CompletedAt.HasValue)
    {
        var duration = (conversion.CompletedAt.Value - conversion.ProcessingStartedAt.Value).TotalSeconds;
        conversionData["processing_duration_seconds"] = (int)duration;
    }

    // ✅ NOTE: output_url NOT included - client should call GET /voice-conversions/{id}
    // This prevents expired URLs from being stored in webhook logs
}
```

### Documentation

Updated `.ai_doc/v1/webhooks.md`:

> **Note:** The `output_url` is NOT included in the webhook payload to prevent expired URLs.
> To download the output file, call `GET /api/v1/voice-conversions/{id}` which returns a fresh pre-signed URL (valid for 12 hours).

---

## Future Extensibility

### Adding New Event Types

When adding new webhook event types (e.g., voice model training, user notifications):

1. **Add constants to `WebhookEventTypes.cs` and `WebhookEntityTypes.cs`**:
```csharp
// In Shared/Domain/WebhookEventTypes.cs
public static class WebhookEventTypes
{
    public const string TrainingCompleted = "training.completed";    // ✅ New
    public const string TrainingFailed = "training.failed";          // ✅ New
}

public static class WebhookEntityTypes
{
    public const string VoiceModelTraining = "voice_model_training"; // ✅ New
}
```

2. **Add enum value** (optional, for backwards compatibility):
```csharp
public enum WebhookEvent
{
    ConversionCompleted,
    ConversionFailed,
    TrainingCompleted,     // ✅ New
    TrainingFailed         // ✅ New
}
```

3. **Use event-agnostic pattern with constants**:
```csharp
var deliveryLog = new WebhookDeliveryLog
{
    EventType = WebhookEventTypes.TrainingCompleted,     // ✅ Type-safe constant
    EntityType = WebhookEntityTypes.VoiceModelTraining,  // ✅ Type-safe constant
    EntityId = training.Id,                              // Generic ID
    Event = WebhookEvent.TrainingCompleted,              // Optional enum
    // ... rest
};
```

4. **Update webhook subscription enum** if needed:
```csharp
// In CreateWebhookSubscriptionDto.cs
public WebhookEvent[] Events { get; set; } = [
    WebhookEvent.ConversionCompleted,
    WebhookEvent.ConversionFailed,
    WebhookEvent.TrainingCompleted,    // ✅ New
    WebhookEvent.TrainingFailed        // ✅ New
];
```

4. **No database migration needed** - EventType/EntityType are strings

### Best Practices Checklist

When implementing any new API endpoint or feature:

- [ ] Use `pitch_shift` abstraction, never expose `Transposition` enum
- [ ] DO NOT include temporary URLs in responses that might be stored
- [ ] Use event-agnostic pattern for webhook events
- [ ] Document all public API fields with user-friendly names
- [ ] Keep internal implementation details (enums, IDs) hidden from external APIs
- [ ] Test that webhook payloads remain valid after 12+ hours

---

## Related Files

### Core Implementation Files
- `Features/VoiceConversions/Application/Helpers/PitchShiftHelper.cs`
- `Features/WebhookSubscriptions/Domain/WebhookDeliveryLog.cs`
- `Features/WebhookSubscriptions/Application/Services/WebhookEventPublisher.cs`
- `Shared/Infrastructure/Data/Configurations/WebhookDeliveryLogConfiguration.cs`

### Documentation
- `.ai_doc/v1/webhooks.md` - Webhook API documentation
- `.ai_doc/v1/voice_conversions.md` - Voice Conversions API documentation
- `.ai_doc/WEBHOOK_ENCRYPTION_COMPLETE.md` - Webhook encryption implementation

### Migrations
- `20251124175636_RefactorWebhookDeliveryLogToBeEventAgnostic.cs`

---

## Summary

**Key Takeaways**:
1. ✅ Always use `pitch_shift` strings in external APIs (never `transposition` enum)
2. ✅ Webhook system is event-agnostic - supports any future event type
3. ✅ Never include temporary URLs in payloads - provide them via API calls
4. ✅ Keep internal implementation details hidden from external APIs

**Status**: All conventions implemented and documented (2025-11-24)
