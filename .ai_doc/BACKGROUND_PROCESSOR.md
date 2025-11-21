# Voice Conversion Background Processor - Implementation Details

## Overview

Voice conversions are processed by a **Background Service** running inside the API application. This replaces the original Lambda-based approach with a simpler, more maintainable solution.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     API Instance 1                          │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  VoiceConversionProcessorService (Background)        │   │
│  │  - Runs every 3 seconds                              │   │
│  │  - Processes pending conversions                     │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
         ↓ (Optimistic Locking prevents conflicts)
┌─────────────────────────────────────────────────────────────┐
│                     API Instance 2                          │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  VoiceConversionProcessorService (Background)        │   │
│  │  - Runs every 3 seconds                              │   │
│  │  - Processes pending conversions                     │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
         ↓
    ┌────────────────┐
    │   PostgreSQL   │ ← RowVersion for concurrency control
    └────────────────┘
         ↓
    ┌────────────────┐
    │   SQS Queue    │ ← Message Deduplication ID
    └────────────────┘
```

## Key Components

### 1. Background Service: `VoiceConversionProcessorService`

**Location:** `Features/VoiceConversions/Application/BackgroundServices/VoiceConversionProcessorService.cs`

**Characteristics:**
- Inherits from `BackgroundService` (ASP.NET Core)
- Runs continuously while the API is running
- Executes every **3 seconds**
- Waits 5 seconds on startup to allow full application initialization
- Handles cancellation gracefully on shutdown

**Lifecycle:**
```csharp
Startup → Wait 5s → Loop (every 3s) → Shutdown
```

### 2. Optimistic Locking (Prevents Race Conditions)

**Problem:** Multiple API instances might try to process the same conversion simultaneously.

**Solution:** Entity Framework's Optimistic Concurrency Control with `RowVersion`.

**How it works:**

1. **Database Column:**
   ```sql
   ALTER TABLE voice_conversions ADD "RowVersion" bytea NOT NULL;
   ```
   - PostgreSQL's `xmin` system column is used automatically via `.IsRowVersion()`
   - EF Core tracks the version on each entity

2. **Processing Flow:**
   ```csharp
   // Step 1: Read without tracking (snapshot of IDs)
   var pendingIds = await context.VoiceConversions
       .AsNoTracking()  // Don't lock yet
       .Where(c => c.Status == ConversionStatus.PendingPreprocessing)
       .Select(c => c.Id)
       .ToListAsync();

   // Step 2: Process each individually
   foreach (var id in pendingIds)
   {
       var conversion = await context.VoiceConversions
           .FirstOrDefaultAsync(c => c.Id == id);  // Now tracking
       
       conversion.Status = ConversionStatus.Queued;
       
       // Step 3: Save with version check
       await context.SaveChangesAsync();  
       // ↑ Throws DbUpdateConcurrencyException if another instance modified it
   }
   ```

3. **Conflict Handling:**
   ```csharp
   catch (DbUpdateConcurrencyException)
   {
       // Another instance already processed this - SKIP (this is OK)
       logger.LogDebug("Concurrency conflict - another instance processing it");
   }
   ```

**Benefits:**
- ✅ No database locks → high performance
- ✅ Safe for multiple instances
- ✅ Conflicts are rare and handled gracefully

### 3. SQS Message Deduplication

**Problem:** Even with Optimistic Locking, there's a small window where two instances might send duplicate SQS messages.

**Solution:** Message Deduplication ID in SQS.

**Implementation:**

```csharp
var deduplicationId = conversion.Id.ToString();  // Use conversion ID
var messageGroupId = conversion.UserId?.ToString() ?? "system";

await sqsService.SendMessageAsync(
    queueUrl, 
    message, 
    deduplicationId,   // ← Prevents duplicates
    messageGroupId     // ← FIFO queue grouping
);
```

**How it works:**
- **FIFO Queues:** SQS rejects duplicate `MessageDeduplicationId` within 5-minute window
- **Standard Queues:** Deduplication ID is ignored by SQS but tracked for client-side logging

**Benefits:**
- ✅ Guaranteed no duplicate messages to external inference service
- ✅ Works even if Optimistic Locking fails
- ✅ 5-minute deduplication window is sufficient for retry intervals

## Processing Logic

### Main Flow: `ProcessPendingConversionsAsync()`

```
1. Query pending conversions (AsNoTracking)
   ↓
2. Take max 10 per batch (avoid long transactions)
   ↓
3. For each conversion:
   ├─→ Load with tracking + related data
   ├─→ Check preprocessing status
   ├─→ Update status accordingly:
   │   ├─→ Preprocessing Failed → Mark conversion as Failed
   │   ├─→ Preprocessing Completed → Queue to SQS
   │   └─→ Preprocessing Pending → Increment retry count
   ├─→ Save changes (Optimistic Locking check)
   └─→ Catch DbUpdateConcurrencyException → Skip (another instance got it)
```

### Retry Mechanism

**Configuration:**
- Max retry attempts: **5**
- Retry delay: **5 minutes**

**Retry logic:**
```csharp
// Only process if:
c.RetryCount < MaxRetryAttempts &&
(c.LastRetryAt == null || 
 c.LastRetryAt.Value.AddMinutes(5) <= UtcNow)
```

**Failure conditions:**
1. Preprocessing fails → Conversion marked as Failed immediately
2. Max retries exceeded → Conversion marked as Failed
3. Exception during processing → Logged, conversion remains pending (will retry)

## Database Schema Changes

### Added Column: `RowVersion`

```sql
-- Migration: 20251119182600_AddRowVersionToVoiceConversions
ALTER TABLE voice_conversions 
ADD "RowVersion" bytea NOT NULL DEFAULT BYTEA E'\\x';
```

**EF Core Configuration:**
```csharp
builder.Property(x => x.RowVersion)
    .IsRowVersion()  // Maps to PostgreSQL's xmin system column
    .IsRequired();
```

## Configuration

### Background Service Settings

**Execution interval:** 3 seconds (hardcoded in `VoiceConversionProcessorService`)

To change the interval, modify:
```csharp
private const int IntervalSeconds = 3;  // Change this value
```

### SQS Queue Configuration

**Required environment variables:**
```bash
AWS__SQS__VoiceInferenceQueueUrl=https://sqs.us-east-1.amazonaws.com/ACCOUNT_ID/queue-name
```

**FIFO vs Standard Queues:**
- **FIFO Queue:** Deduplication enabled by SQS (recommended)
- **Standard Queue:** Deduplication ID logged but not enforced by SQS

## Testing Scenarios

### Scenario 1: Single API Instance
```
✓ Conversions processed every 3 seconds
✓ No concurrency conflicts
✓ No duplicate SQS messages
```

### Scenario 2: Multiple API Instances (e.g., 3 instances)
```
Instance 1: Reads 10 pending conversions
Instance 2: Reads same 10 pending conversions (same moment)
Instance 3: Reads same 10 pending conversions (same moment)

Processing:
- Instance 1 processes Conversion #1 → SaveChanges → SUCCESS
- Instance 2 tries Conversion #1 → SaveChanges → DbUpdateConcurrencyException → SKIP
- Instance 3 tries Conversion #1 → SaveChanges → DbUpdateConcurrencyException → SKIP

Result: Only Instance 1's message sent to SQS ✓
```

### Scenario 3: Race Condition (both instances save before conflict detection)

**Unlikely but possible:**
```
Instance 1: Updates Conversion #1 → SaveChanges (T=0ms)
Instance 2: Updates Conversion #1 → SaveChanges (T=1ms, RowVersion conflict)

Result: Instance 2 fails → Conversion already queued ✓
```

**If both somehow succeed (extremely rare):**
```
Instance 1: SendMessageAsync(deduplicationId="abc-123")
Instance 2: SendMessageAsync(deduplicationId="abc-123")

SQS FIFO Queue: Accepts first message, rejects second ✓
```

## Monitoring & Logging

### Log Messages

**Background service lifecycle:**
```
[Information] VoiceConversionProcessorService started. Processing every 3 seconds
[Information] VoiceConversionProcessorService stopped
```

**Processing logs:**
```
[Information] Found {Count} pending conversions to process
[Information] Background processing completed: Total=10, Processed=8, Skipped=2
```

**Concurrency conflicts (expected, not errors):**
```
[Debug] Concurrency conflict for ConversionId={Id} - another instance is processing it
```

**Errors:**
```
[Error] Error processing pending conversion: ConversionId={Id}
[Warning] Conversion failed - max retries exceeded: ConversionId={Id}
```

### Metrics to Monitor

1. **Processing rate:** Conversions processed per minute
2. **Retry count:** Average retry count before success
3. **Concurrency conflicts:** Frequency of `DbUpdateConcurrencyException`
4. **Failed conversions:** Conversions marked as Failed

### CloudWatch Logs (when deployed)

Filter by:
- `"VoiceConversionProcessorService"` → Background service logs
- `"Concurrency conflict"` → Race conditions (should be rare)
- `"max retries exceeded"` → Conversions that gave up

## Deployment Considerations

### AWS App Runner (Current Deployment)

**Scaling:**
- App Runner can scale to multiple instances automatically
- Each instance runs its own background service
- Optimistic Locking ensures no conflicts

**Memory/CPU:**
- Background service is lightweight (sleeps 3 seconds between runs)
- No significant resource impact

### Docker Compose (Local Development)

**Single instance:**
```bash
docker-compose up
```
- Background service starts automatically
- Processes conversions every 3 seconds

**Multiple instances (testing concurrency):**
```bash
docker-compose up --scale api=3
```
- Three API instances with separate background services
- Test Optimistic Locking behavior

## Advantages over Lambda Approach

| Aspect | Lambda | Background Service |
|--------|--------|-------------------|
| **Deployment** | Separate Lambda + EventBridge | Built into API ✓ |
| **Configuration** | Duplicate env vars | Shared with API ✓ |
| **Debugging** | CloudWatch Logs only | IDE debugging ✓ |
| **Cold starts** | Yes (5+ seconds) | No ✓ |
| **Shared code** | Tricky (project references) | Same codebase ✓ |
| **Secrets** | Manual env vars | Shared Secrets Manager ✓ |
| **Complexity** | High | Low ✓ |
| **Cost** | Lambda invocations + EventBridge | Included in API ✓ |

## Migration from Lambda (If Applicable)

**Note:** The Lambda project and deployment workflow have been removed from this repository as of November 19, 2025.

If you previously deployed the Lambda to AWS, you should clean up the resources:

1. **Disable EventBridge rule:**
   ```bash
   aws events disable-rule --name VoiceConversionProcessorSchedule
   ```

2. **Remove Lambda function:**
   ```bash
   aws lambda delete-function --function-name VoiceByAuribusApiConversionProcessor
   ```

3. **Delete ECR images:**
   ```bash
   aws ecr batch-delete-image \
     --repository-name voice-by-auribus-api/conversion-processor \
     --image-ids imageTag=latest
   ```

4. **Delete ECR repository (optional):**
   ```bash
   aws ecr delete-repository \
     --repository-name voice-by-auribus-api/conversion-processor \
     --force
   ```

## Troubleshooting

### Background service not running

**Check logs:**
```bash
# Look for startup message
grep "VoiceConversionProcessorService started" logs.txt
```

**Verify registration:**
```csharp
// VoiceConversionsModule.cs should have:
services.AddHostedService<VoiceConversionProcessorService>();
```

### Conversions stuck in PendingPreprocessing

**Check:**
1. Background service is running
2. Preprocessing completed successfully
3. Retry count < 5
4. LastRetryAt is older than 5 minutes

**Manually trigger:**
```bash
# Restart API to restart background service
docker-compose restart api
```

### High concurrency conflicts

**Normal rate:** < 5% of conversions

**High rate (>20%):**
- Too many API instances for the workload
- Consider increasing processing interval to 5-10 seconds

### Duplicate SQS messages

**FIFO Queue:** Should never happen (SQS enforces deduplication)

**Standard Queue:** Possible but extremely rare (AWS best-effort deduplication)

**Check deduplication IDs in logs:**
```
"Conversion message sent to SQS with deduplication: ConversionId={Id}, DeduplicationId={DeduplicationId}"
```

## Future Enhancements

### Dynamic Interval Adjustment

Currently hardcoded to 3 seconds. Could be made configurable:

```csharp
// appsettings.json
"VoiceConversions": {
  "BackgroundProcessor": {
    "IntervalSeconds": 3
  }
}
```

### Processing Batch Size

Currently processes max 10 conversions per batch. Could be configurable:

```csharp
.Take(batchSize)  // From configuration
```

### Metrics/Telemetry

Add custom metrics for monitoring:
- Conversions processed per minute
- Average retry count
- Concurrency conflict rate

### Dead Letter Queue

For conversions that fail max retries, optionally send to DLQ for manual review.
