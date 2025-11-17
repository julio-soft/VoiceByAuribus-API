# Logging y CloudWatch - Guía de Uso

## Resumen de Mejoras Implementadas

Este documento describe las mejoras implementadas en el sistema de logging del proyecto VoiceByAuribus-API, optimizado para AWS CloudWatch Logs.

## ✅ Cambios Realizados

### 1. **Migración a Serilog con Formato JSON Estructurado**

**Antes:**
```csharp
Console.WriteLine("[STARTUP] Starting application");
_logger.LogError(ex, "An unhandled exception occurred");
```

**Ahora:**
```csharp
Log.Information("Starting application");
_logger.LogError(ex, 
    "Unhandled exception: {ExceptionType} | Path: {RequestPath} | Method: {RequestMethod}",
    ex.GetType().Name, context.Request.Path, context.Request.Method);
```

### 2. **Configuración de Serilog**

#### Producción (`appsettings.json`)
- **Formato:** Compact JSON (`CompactJsonFormatter`)
- **Nivel:** Information
- **Enrichers:** FromLogContext, MachineName, ThreadId, EnvironmentName
- **Beneficios CloudWatch:**
  - Logs parseables automáticamente
  - Búsquedas y filtros eficientes
  - Métricas y alertas basadas en campos estructurados

#### Desarrollo (`appsettings.Development.json`)
- **Formato:** Texto legible con colores
- **Nivel:** Debug
- **Ideal para:** Debugging local

### 3. **Logging Estructurado en Servicios**

#### AudioFileService
```csharp
logger.LogInformation(
    "Creating audio file: FileName={FileName}, MimeType={MimeType}, UserId={UserId}",
    dto.FileName, dto.MimeType, userId);

logger.LogInformation(
    "Audio file created successfully: AudioFileId={AudioFileId}, S3Uri={S3Uri}",
    audioFile.Id, audioFile.S3Uri);
```

#### AudioPreprocessingService
```csharp
logger.LogInformation(
    "Triggering audio preprocessing: AudioFileId={AudioFileId}",
    audioFileId);

logger.LogInformation(
    "Preprocessing completed successfully: AudioFileId={AudioFileId}, Duration={Duration}s",
    audioFile.Id, dto.AudioDuration.Value);
```

#### VoiceModelService
```csharp
logger.LogInformation("Fetching all voice models");
logger.LogInformation("Retrieved {Count} voice models", voices.Count);
```

### 4. **Request/Response Logging**

Configurado en `Program.cs`:
```csharp
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
        diagnosticContext.Set("RemoteIP", httpContext.Connection.RemoteIpAddress);
    };
});
```

**Ejemplo de log generado:**
```json
{
  "@t": "2025-11-17T10:30:45.123Z",
  "@mt": "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms",
  "RequestMethod": "POST",
  "RequestPath": "/api/v1/audio-files",
  "StatusCode": 201,
  "Elapsed": 45.6789,
  "RequestHost": "api.voicebyauribus.com",
  "UserAgent": "Mozilla/5.0...",
  "RemoteIP": "192.168.1.1"
}
```

### 5. **Logging en Controllers con Contexto**

```csharp
_logger.LogInformation(
    "[API] POST /audio-files - UserId={UserId}, FileName={FileName}",
    userId, dto.FileName);

_logger.LogInformation(
    "[WEBHOOK] POST /audio-files/webhook/upload-notification - S3Uri={S3Uri}, FileSize={FileSize}",
    dto.S3Uri, dto.FileSize);
```

### 6. **Manejo de Errores con Contexto Rico**

GlobalExceptionHandlerMiddleware:
```csharp
_logger.LogError(ex, 
    "Unhandled exception: {ExceptionType} | Path: {RequestPath} | Method: {RequestMethod} | TraceId: {TraceId}",
    ex.GetType().Name,
    context.Request.Path,
    context.Request.Method,
    context.TraceIdentifier);
```

## 📊 Consultas Útiles en CloudWatch Logs Insights

### 1. Errores en las últimas 24 horas
```cloudwatch
fields @timestamp, @message, ExceptionType, RequestPath, RequestMethod, TraceId
| filter @level = "Error"
| sort @timestamp desc
| limit 100
```

### 2. Rendimiento de endpoints (tiempo de respuesta > 1s)
```cloudwatch
fields @timestamp, RequestMethod, RequestPath, StatusCode, Elapsed
| filter Elapsed > 1000
| sort Elapsed desc
| limit 50
```

### 3. Actividad por usuario
```cloudwatch
fields @timestamp, @message, UserId, @logStream
| filter UserId like /[0-9a-f-]{36}/
| stats count() by UserId
| sort count desc
```

### 4. Seguimiento de pipeline de audio processing
```cloudwatch
fields @timestamp, @message, AudioFileId, ProcessingStatus
| filter @message like /preprocessing/ or @message like /upload notification/
| sort @timestamp asc
```

### 5. Webhooks recibidos
```cloudwatch
fields @timestamp, @message, S3Uri, FileSize, AudioDuration
| filter @message like /WEBHOOK/
| sort @timestamp desc
```

### 6. Errores por tipo
```cloudwatch
fields @timestamp, ExceptionType, @message
| filter @level = "Error"
| stats count() by ExceptionType
| sort count desc
```

### 7. Tráfico por endpoint
```cloudwatch
fields @timestamp, RequestMethod, RequestPath, StatusCode
| filter RequestPath like /api/
| stats count() by RequestPath, RequestMethod
| sort count desc
```

### 8. Audio files creados por día
```cloudwatch
fields @timestamp, AudioFileId, FileName, UserId
| filter @message like /Audio file created successfully/
| stats count() by bin(@timestamp, 1d)
```

## 🔍 Filtros CloudWatch para Alertas

### Alerta: Tasa alta de errores
```cloudwatch
[level = Error]
```
**Métrica:** Contar ocurrencias > 10 en 5 minutos

### Alerta: Tiempos de respuesta lentos
```cloudwatch
[Elapsed > 5000]
```
**Métrica:** Contar ocurrencias > 5 en 1 minuto

### Alerta: Fallos en preprocessing
```cloudwatch
[ProcessingStatus = Failed]
```
**Métrica:** Contar ocurrencias > 3 en 10 minutos

### Alerta: Webhook failures
```cloudwatch
[WEBHOOK && level = Error]
```
**Métrica:** Contar ocurrencias > 2 en 5 minutos

## 🎯 Mejores Prácticas

### 1. **Niveles de Log**
- `LogTrace`: Información muy detallada (disabled en producción)
- `LogDebug`: Información de debugging (disabled en producción)
- `LogInformation`: Eventos importantes del flujo (operaciones exitosas)
- `LogWarning`: Situaciones anómalas pero recuperables
- `LogError`: Errores que requieren atención
- `LogCritical`: Fallos críticos del sistema

### 2. **Campos Estructurados Clave**
Siempre incluir cuando sea relevante:
- `UserId`: Para rastrear actividad por usuario
- `AudioFileId`: Para seguir el ciclo de vida de archivos
- `RequestPath` y `RequestMethod`: Para contexto de API
- `TraceId`: Para correlacionar logs de una misma petición
- `S3Uri`: Para operaciones de almacenamiento
- `Duration`/`Elapsed`: Para métricas de rendimiento

### 3. **Mensajes Descriptivos**
```csharp
// ✅ BIEN: Descriptivo con contexto
logger.LogInformation(
    "Audio file created successfully: AudioFileId={AudioFileId}, S3Uri={S3Uri}",
    audioFile.Id, audioFile.S3Uri);

// ❌ MAL: Genérico sin contexto
logger.LogInformation("File created");
```

### 4. **Errores con Stack Trace**
```csharp
// ✅ BIEN: Excepción incluida
logger.LogError(ex, 
    "Upload notification failed: S3Uri={S3Uri}",
    s3Uri);

// ❌ MAL: Solo mensaje
logger.LogError("Upload failed");
```

### 5. **Logging en Transacciones**
```csharp
// Inicio de operación
logger.LogInformation("Starting operation: {OperationId}", operationId);

// Pasos intermedios si son relevantes
logger.LogDebug("Intermediate step completed: {Step}", stepName);

// Resultado final
logger.LogInformation("Operation completed: {OperationId}, Result={Result}", 
    operationId, result);
```

## 📦 Paquetes NuGet Instalados

```xml
<PackageReference Include="Serilog.AspNetCore" Version="8.0.3" />
<PackageReference Include="Serilog.Enrichers.Environment" Version="3.1.0" />
<PackageReference Include="Serilog.Enrichers.Thread" Version="4.0.0" />
<PackageReference Include="Serilog.Formatting.Compact" Version="3.0.0" />
<PackageReference Include="Serilog.Settings.Configuration" Version="8.0.4" />
```

## 🚀 Ejemplo de Log Completo en CloudWatch

```json
{
  "@t": "2025-11-17T14:23:45.1234567Z",
  "@mt": "Audio file created successfully: AudioFileId={AudioFileId}, S3Uri={S3Uri}",
  "@l": "Information",
  "AudioFileId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "S3Uri": "s3://voice-by-auribus-api/audio-files/user123/temp/file.mp3",
  "SourceContext": "VoiceByAuribus_API.Features.AudioFiles.Application.Services.AudioFileService",
  "MachineName": "ip-10-0-1-123",
  "ThreadId": 42,
  "EnvironmentName": "Production",
  "Application": "VoiceByAuribus-API"
}
```

## 🔧 Troubleshooting

### Problema: Los logs no aparecen en formato JSON en CloudWatch
**Solución:** Verificar que `appsettings.json` en producción usa `CompactJsonFormatter`:
```json
"WriteTo": [
  {
    "Name": "Console",
    "Args": {
      "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact"
    }
  }
]
```

### Problema: Demasiados logs de EF Core
**Solución:** Ajustar nivel en `appsettings.json`:
```json
"Override": {
  "Microsoft.EntityFrameworkCore": "Warning"
}
```

### Problema: Falta contexto de usuario en logs
**Solución:** Verificar que `ICurrentUserService` está correctamente inyectado y el usuario está autenticado.

## 📈 Métricas Recomendadas en CloudWatch

1. **Error Rate:** Porcentaje de requests que resultan en error
2. **P95 Response Time:** 95º percentil de tiempos de respuesta
3. **Audio Processing Success Rate:** % de procesamiento exitoso
4. **Webhook Delivery Rate:** % de webhooks procesados exitosamente
5. **Active Users:** Usuarios únicos por período de tiempo

## 🔐 Seguridad en Logs

**⚠️ NUNCA loguear:**
- Tokens de autenticación
- Contraseñas
- Claves API
- Datos sensibles de usuarios (correos, teléfonos sin sanitizar)

**✅ Sí loguear:**
- IDs de recursos
- URIs de S3 (no contienen datos sensibles)
- Metadatos de operaciones
- Tiempos y estados de procesamiento
