# Ejemplos Prácticos de Logging - CloudWatch

## 🎯 Casos de Uso Comunes

### 1. Debugging de un Error en Producción

**Escenario:** Un usuario reporta que su archivo de audio no se procesó correctamente.

#### Paso 1: Buscar por UserId
```cloudwatch
fields @timestamp, @message, AudioFileId, UploadStatus, ProcessingStatus
| filter UserId = "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
| sort @timestamp asc
```

#### Paso 2: Seguir el AudioFileId específico
```cloudwatch
fields @timestamp, @message, @level
| filter AudioFileId = "file-123-456"
| sort @timestamp asc
```

**Output esperado:**
```
10:23:45 [INFO]  Creating audio file: FileName=song.mp3, UserId=...
10:23:46 [INFO]  Audio file created successfully: AudioFileId=file-123-456
10:25:12 [INFO]  Processing upload notification: S3Uri=s3://...
10:25:13 [INFO]  Upload notification processed: AudioFileId=file-123-456
10:25:14 [INFO]  Triggering audio preprocessing: AudioFileId=file-123-456
10:25:15 [INFO]  Preprocessing message sent to SQS
10:35:20 [WARN]  Preprocessing failed: AudioFileId=file-123-456, Reason=Audio duration not provided
```

**Diagnóstico:** El servicio de preprocessing no devolvió la duración del audio. Revisar logs del servicio externo.

---

### 2. Análisis de Rendimiento

**Escenario:** Algunos usuarios reportan lentitud en la API.

#### Query: Endpoints más lentos
```cloudwatch
fields @timestamp, RequestMethod, RequestPath, Elapsed, StatusCode
| filter Elapsed > 1000
| stats avg(Elapsed) as AvgMs, max(Elapsed) as MaxMs, count() as Count by RequestPath
| sort AvgMs desc
```

**Output:**
```
RequestPath                                    AvgMs    MaxMs    Count
/api/v1/audio-files                           2345.6   4567.8   15
/api/v1/voices                                1234.5   2345.6   8
/api/v1/audio-files/{id}                      890.4    1500.2   5
```

**Acción:** Optimizar endpoint `/audio-files` (posiblemente agregar caché o índices en BD).

#### Query: Distribución de latencia
```cloudwatch
fields Elapsed
| stats 
    avg(Elapsed) as AvgMs,
    percentile(Elapsed, 50) as P50,
    percentile(Elapsed, 95) as P95,
    percentile(Elapsed, 99) as P99
```

---

### 3. Monitoreo de Webhooks

**Escenario:** Verificar que los webhooks de S3 se están recibiendo correctamente.

#### Query: Webhooks recibidos en las últimas 24h
```cloudwatch
fields @timestamp, S3Uri, FileSize, AudioFileId
| filter @message like /WEBHOOK.*upload-notification/
| sort @timestamp desc
```

#### Query: Tasa de éxito de webhooks
```cloudwatch
fields @timestamp, @level
| filter @message like /WEBHOOK/
| stats count() as Total, 
        sum(@level = "Information") as Success,
        sum(@level = "Error") as Failures
| extend SuccessRate = (Success * 100.0 / Total)
```

**Output:**
```
Total: 1250
Success: 1242
Failures: 8
SuccessRate: 99.36%
```

---

### 4. Auditoría de Operaciones por Usuario

**Escenario:** Auditoría de seguridad - revisar todas las operaciones de un usuario específico.

```cloudwatch
fields @timestamp, @message, RequestPath, RequestMethod, StatusCode
| filter UserId = "user-abc-123"
| sort @timestamp desc
| limit 500
```

**Extender con detalles:**
```cloudwatch
fields @timestamp, @message, AudioFileId, FileName, UploadStatus
| filter UserId = "user-abc-123" and @message like /Audio file created/
| stats count() as FilesCreated by bin(@timestamp, 1d)
```

---

### 5. Identificar Picos de Tráfico

```cloudwatch
fields @timestamp
| stats count() as Requests by bin(@timestamp, 5m)
| sort Requests desc
```

**Visualización:**
```
Timestamp              Requests
2025-11-17 14:30:00    345
2025-11-17 14:35:00    298
2025-11-17 14:40:00    267
...
```

---

### 6. Análisis de Errores por Tipo

```cloudwatch
fields @timestamp, ExceptionType, RequestPath, @message
| filter @level = "Error"
| stats count() as ErrorCount by ExceptionType, RequestPath
| sort ErrorCount desc
```

**Output:**
```
ExceptionType                  RequestPath                    ErrorCount
InvalidOperationException      /api/v1/audio-files/{id}      12
DbUpdateException              /api/v1/audio-files           5
TimeoutException               /api/v1/voices                2
```

---

### 7. Tracking de Pipeline de Audio Processing

**Escenario:** Ver el estado completo del pipeline para un archivo.

```cloudwatch
fields @timestamp, @message, ProcessingStatus, AudioDuration
| filter AudioFileId = "file-789"
| sort @timestamp asc
```

**Output esperado (exitoso):**
```
10:00:00  Creating audio file: FileName=test.mp3
10:00:01  Audio file created successfully: AudioFileId=file-789
10:02:30  Processing upload notification: S3Uri=s3://...
10:02:31  Upload notification processed: Status=Uploaded
10:02:32  Triggering audio preprocessing
10:02:33  Preprocessing message sent to SQS
10:07:45  Processing preprocessing result: AudioDuration=125.5
10:07:46  Preprocessing completed successfully: Duration=125.5s
```

---

### 8. Alertas Proactivas

#### Configuración de Alarma: Alta Tasa de Errores
```cloudwatch
# Métrica personalizada
fields @timestamp
| filter @level = "Error" and RequestPath like /api/v1/
| stats count() as ErrorCount by bin(@timestamp, 5m)
```

**Condición de Alarma:**
- ErrorCount > 10 en ventana de 5 minutos
- 2 de 3 períodos consecutivos

#### Configuración de Alarma: Latencia Alta
```cloudwatch
fields Elapsed
| filter Elapsed > 2000
| stats count() as SlowRequests by bin(@timestamp, 5m)
```

**Condición de Alarma:**
- SlowRequests > 5 en ventana de 5 minutos

---

### 9. Análisis de Voice Models Más Utilizados

```cloudwatch
fields @timestamp, VoiceModelId, Name
| filter @message like /Voice model retrieved/
| stats count() as Accesses by VoiceModelId, Name
| sort Accesses desc
| limit 10
```

---

### 10. Detección de Intentos de Acceso No Autorizado

```cloudwatch
fields @timestamp, RequestPath, StatusCode, RemoteIP
| filter StatusCode = 401 or StatusCode = 403
| stats count() as AttemptCount by RemoteIP, RequestPath
| sort AttemptCount desc
```

**Acción:** Si `AttemptCount` > 50 desde una IP, considerar bloqueo.

---

## 🔔 Configuración de Dashboards Recomendados

### Dashboard 1: API Health
**Widgets:**
1. **Error Rate (últimas 24h)**
   ```cloudwatch
   fields @timestamp
   | filter @level = "Error"
   | stats count() as Errors by bin(@timestamp, 1h)
   ```

2. **Request Volume**
   ```cloudwatch
   fields @timestamp
   | stats count() as Requests by bin(@timestamp, 1h)
   ```

3. **P95 Latency**
   ```cloudwatch
   fields Elapsed
   | stats percentile(Elapsed, 95) as P95 by bin(@timestamp, 1h)
   ```

4. **Status Code Distribution**
   ```cloudwatch
   fields StatusCode
   | stats count() by StatusCode
   ```

### Dashboard 2: Audio Processing Pipeline
**Widgets:**
1. **Files Uploaded (últimas 24h)**
   ```cloudwatch
   fields @timestamp
   | filter @message like /Audio file created/
   | stats count() by bin(@timestamp, 1h)
   ```

2. **Processing Success Rate**
   ```cloudwatch
   fields ProcessingStatus
   | filter @message like /Preprocessing/
   | stats count() by ProcessingStatus
   ```

3. **Average Processing Time**
   ```cloudwatch
   fields @timestamp, ProcessingStartedAt, ProcessingCompletedAt
   | filter ProcessingStatus = "Completed"
   | stats avg(ProcessingCompletedAt - ProcessingStartedAt) by bin(@timestamp, 1h)
   ```

### Dashboard 3: User Activity
**Widgets:**
1. **Active Users (últimas 24h)**
   ```cloudwatch
   fields UserId
   | filter UserId like /[0-9a-f-]{36}/
   | stats dc(UserId) as UniqueUsers by bin(@timestamp, 1h)
   ```

2. **Top Users by Activity**
   ```cloudwatch
   fields UserId
   | stats count() as Actions by UserId
   | sort Actions desc
   | limit 10
   ```

---

## 🎓 Tips Avanzados

### 1. Correlación de Logs con TraceId
```cloudwatch
fields @timestamp, @message, SourceContext
| filter TraceId = "0HN1234567890ABCDEFG"
| sort @timestamp asc
```

### 2. Buscar Excepciones Específicas con Stack Trace
```cloudwatch
fields @timestamp, @message, ExceptionType
| filter @message like /NullReferenceException/
```

### 3. Análisis de Patrones de Usuario
```cloudwatch
fields UserId, RequestPath
| filter UserId = "user-123"
| stats count() as AccessCount by RequestPath
| sort AccessCount desc
```

### 4. Detección de Anomalías
```cloudwatch
fields @timestamp
| filter @level = "Error"
| stats count() as ErrorCount by bin(@timestamp, 5m)
| sort @timestamp desc
```
Comparar con promedio histórico para detectar picos anormales.

---

## 📋 Checklist de Troubleshooting

Cuando hay un problema en producción:

1. ✅ **Identificar el período de tiempo** del problema
2. ✅ **Buscar errores** en ese período (`@level = "Error"`)
3. ✅ **Correlacionar por TraceId** para seguir una request completa
4. ✅ **Revisar logs de servicios relacionados** (AudioFileService, AudioPreprocessingService)
5. ✅ **Verificar webhooks** si es un problema de integración
6. ✅ **Analizar latencias** si es un problema de rendimiento
7. ✅ **Revisar cambios recientes** en el código (deployments)

---

## 🚨 Alertas Críticas Recomendadas

### 1. Error Rate > 5%
```plaintext
Métrica: (ErrorCount / TotalRequests) * 100
Threshold: > 5%
Duración: 5 minutos
Acción: Notificar equipo de guardia
```

### 2. P95 Latency > 3 segundos
```plaintext
Métrica: percentile(Elapsed, 95)
Threshold: > 3000ms
Duración: 10 minutos
Acción: Revisar rendimiento de BD y servicios externos
```

### 3. Preprocessing Failures > 10%
```plaintext
Métrica: (FailedCount / TotalProcessing) * 100
Threshold: > 10%
Duración: 15 minutos
Acción: Verificar servicio de preprocessing y SQS
```

### 4. Webhook Delivery Failures
```plaintext
Métrica: Count de WEBHOOK + Error
Threshold: > 5 en 10 minutos
Acción: Verificar Lambda y permisos de S3
```
