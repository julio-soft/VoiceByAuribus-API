# Voice Conversion Feature - Implementation Summary

## ✅ Completado

Se ha implementado completamente la funcionalidad de conversión de voz (Voice Conversions) siguiendo la arquitectura Vertical Slice del proyecto.

## 📁 Estructura de Archivos Creados

### Domain Layer
```
Features/VoiceConversions/Domain/
├── VoiceConversion.cs          # Entidad principal con auditoría y user ownership
├── Transposition.cs            # Enum con opciones de transposición
└── ConversionStatus.cs         # Enum con estados del proceso
```

### Application Layer
```
Features/VoiceConversions/Application/
├── Dtos/
│   ├── CreateVoiceConversionDto.cs           # Input para crear conversión
│   ├── VoiceConversionResponseDto.cs         # Output con datos de conversión
│   └── VoiceConversionWebhookDto.cs          # Input del webhook externo
├── Mappers/
│   └── VoiceConversionMapper.cs              # Mapper estático con soporte admin
└── Services/
    ├── IVoiceConversionService.cs            # Interfaz del servicio
    └── VoiceConversionService.cs             # Lógica de negocio completa
```

### Presentation Layer
```
Features/VoiceConversions/Presentation/Controllers/
├── VoiceConversionsController.cs             # Endpoints principales (POST, GET)
└── VoiceConversionsWebhookController.cs      # Webhook para resultados
```

### Infrastructure
```
Shared/Infrastructure/Data/Configurations/
└── VoiceConversionConfiguration.cs           # EF Core configuration

VoiceConversions/
└── VoiceConversionsModule.cs                 # Registro DI
```

### Background Processing (Lambda)
```
VoiceByAuribus.ConversionProcessor/
├── src/VoiceByAuribus.ConversionProcessor/
│   ├── Function.cs                           # Lambda handler
│   ├── VoiceByAuribus.ConversionProcessor.csproj
│   ├── appsettings.json
│   ├── Dockerfile                            # Container image definition
│   └── .dockerignore
├── deploy-lambda.sh                          # Automated deployment script
├── build-local.sh                            # Local build/test script
└── README.md                                 # Deployment documentation
```

### Documentation
```
.ai_doc/v1/
└── voice_conversions.md                      # Documentación completa de la API
```

## 🔄 Flujo de Trabajo

### 1. Cliente Crea Conversión
```
POST /api/v1/voice-conversions
{
  "audio_file_id": "uuid",
  "voice_model_id": "uuid",
  "transposition": "SameOctave"
}
```

**Validaciones:**
- ✅ AudioFile existe y pertenece al usuario
- ✅ VoiceModel existe
- ✅ Preprocessing del audio no está en estado Failed

**Estados iniciales:**
- **PendingPreprocessing**: Si el audio aún no está procesado
- **Queued**: Si el audio ya está procesado → envía mensaje a SQS inmediatamente

### 2. Background Processor (Lambda)
**Trigger:** EventBridge cada 5 minutos

**Acciones:**
1. Busca conversiones en estado `PendingPreprocessing`
2. Verifica estado del preprocessing del audio
3. Si preprocessing completado → envía a cola SQS
4. Si preprocessing falló → marca conversión como fallida
5. Sistema de reintentos: máximo 5 intentos con delay de 5 minutos

### 3. Servicio Externo Procesa

**Selección de Cola SQS:**
| Condición | Cola |
|-----------|------|
| `use_preview = true` | `voice-by-auribus-inference-paid-preview` |
| `transposition = 0` (SameOctave) | `voice-by-auribus-inference-paid-main` |
| `transposition != 0` (pitch shift) | `voice-by-auribus-inference-paid-alt` |

**Mensaje SQS enviado:**
```json
{
  "request_id": "conversion-uuid",
  "voice_model_path": "s3://bucket/models/model.pth",
  "voice_model_index_path": "s3://bucket/models/model.index",
  "transposition": 0,
  "s3_uri_in": "s3://bucket/audio-files/{userId}/inference/{fileId}.mp3",
  "s3_uri_out": "s3://bucket/audio-files/{userId}/converted/{fileId}_{conversionId}.mp3",
  "callback_response": {
    "url": "https://api.example.com/webhooks/conversion-result",
    "type": "HTTP"
  }
}
```

**Campos del mensaje:**
- `request_id`: ID de la conversión (GUID como string)
- `transposition`: Valor de transposición en semitonos (integer)
- `voice_model_path`: S3 URI del modelo de voz
- `voice_model_index_path`: S3 URI del índice del modelo
- `s3_uri_in`: S3 URI del audio de entrada (preprocesado)
- `s3_uri_out`: S3 URI donde guardar el audio convertido
- `callback_response`: (opcional) Configuración del webhook de respuesta

### 4. Webhook de Resultado
```
POST /api/v1/voice-conversions/webhooks/conversion-result
X-Webhook-Api-Key: {api_key}
{
  "status": "SUCCESS",
  "request_id": "conversion-uuid",
  "finished_at_utc": "2025-11-29T15:30:00Z"
}
```

**Campos de respuesta:**
- `status`: Resultado del procesamiento - "SUCCESS" o "FAILED"
- `request_id`: ID de la conversión original (GUID como string)
- `finished_at_utc`: Timestamp ISO 8601 UTC cuando terminó el procesamiento

**Actualiza:**
- Status → `Completed` o `Failed`
- `completed_at` timestamp
- `error_message` si status es "FAILED"

## 🗄️ Base de Datos

**Migración aplicada:** `20251117202104_AddVoiceConversions`

**Tabla:** `voice_conversions`
- Primary Key: Id (uuid)
- Foreign Keys: AudioFileId, VoiceModelId
- Índices: UserId, AudioFileId, VoiceModelId, Status, (Status, RetryCount)
- Soft Delete: IsDeleted
- Auditoría: CreatedAt, UpdatedAt
- User Ownership: UserId (filtrado automático)

## 🔐 Seguridad

### Autenticación
- Todos los endpoints requieren JWT token con scope `voice-by-auribus-api/base`
- User ownership automático via `IHasUserId`
- Global filter asegura que usuarios solo vean sus conversiones

### Admin Data
Campos visibles solo para usuarios con scope admin:
- `output_s3_uri`: URI completa del S3
- `retry_count`: Número de reintentos

### Webhooks
- Endpoint protegido con `WebhookAuthenticationAttribute`
- Valida header `X-Webhook-Api-Key` contra `Webhooks:ApiKey` en configuración

## ⚙️ Configuración

### appsettings.json
```json
{
  "AWS": {
    "S3": {
      "AudioFilesBucket": "voice-by-auribus-api"
    },
    "SQS": {
      "PreviewInferenceQueue": "voice-by-auribus-inference-paid-preview",
      "MainInferenceQueue": "voice-by-auribus-inference-paid-main",
      "AltInferenceQueue": "voice-by-auribus-inference-paid-alt",
      "VoiceConversionCallbackUrl": "https://api.example.com/api/v1/voice-conversions/webhooks/conversion-result",
      "VoiceConversionCallbackType": "HTTP"
    }
  }
}
```

### Variables de Entorno (Lambda)
```
ConnectionStrings__DefaultConnection={connection_string}
AWS__Region=us-east-1
AWS__S3__AudioFilesBucket=voice-by-auribus-api
AWS__SQS__PreviewInferenceQueue=voice-by-auribus-inference-paid-preview
AWS__SQS__MainInferenceQueue=voice-by-auribus-inference-paid-main
AWS__SQS__AltInferenceQueue=voice-by-auribus-inference-paid-alt
AWS__SQS__VoiceConversionCallbackUrl={callback_url}
AWS__SQS__VoiceConversionCallbackType=HTTP
```

## 🚀 Deployment

### API (App Runner)
La feature ya está registrada en `Program.cs`:
```csharp
builder.Services.AddVoiceConversionsFeature();
```

### Lambda (.NET 10 como Contenedor Docker)

**¿Por qué contenedor?**
AWS Lambda no tiene soporte nativo para .NET 10. El deployment como contenedor Docker permite:
- ✅ Usar .NET 10 (consistente con la API principal)
- ✅ Control total sobre dependencias y runtime
- ✅ Facilita futuras actualizaciones de .NET
- ✅ Performance similar a runtimes nativos

**Deployment Automatizado:**
```bash
cd VoiceByAuribus.ConversionProcessor
./deploy-lambda.sh
```

El script ejecuta:
1. Build del proyecto .NET 10
2. Crea/verifica repositorio ECR
3. Autentica Docker con ECR
4. Build de imagen Docker
5. Push a ECR
6. Actualiza código de Lambda function

**Build Local (para testing):**
```bash
cd VoiceByAuribus.ConversionProcessor
./build-local.sh

# Luego probar con:
docker run --rm -p 9000:8080 voice-by-auribus-conversion-processor:local
curl -XPOST 'http://localhost:9000/2015-03-31/functions/function/invocations' -d '{}'
```

**Deployment Manual:**
Ver instrucciones detalladas en `VoiceByAuribus.ConversionProcessor/README.md`

### EventBridge Rule
Crear regla para ejecutar Lambda cada 5 minutos:
```json
{
  "ScheduleExpression": "rate(5 minutes)",
  "State": "ENABLED",
  "Targets": [{
    "Arn": "arn:aws:lambda:REGION:ACCOUNT:function:VoiceByAuribusConversionProcessor",
    "Id": "ConversionProcessorTarget"
  }]
}
```

## 📊 Opciones de Transposición

| Enum Value | Semitones | Descripción |
|-----------|-----------|-------------|
| SameOctave | 0 | Sin cambio de tono |
| LowerOctave | -12 | Una octava más grave |
| HigherOctave | +12 | Una octava más aguda |
| ThirdDown | -4 | Tercera menor hacia abajo |
| ThirdUp | +4 | Tercera mayor hacia arriba |
| FifthDown | -7 | Quinta justa hacia abajo |
| FifthUp | +7 | Quinta justa hacia arriba |

## 🔍 Monitoreo

### CloudWatch Logs
**Lambda:**
- Número de conversiones pendientes procesadas
- Conversiones enviadas a cola exitosamente
- Conversiones fallidas por preprocessing
- Errores durante procesamiento

**API:**
- Creación de conversiones
- Actualizaciones de webhook
- Errores de validación

### Métricas Importantes
- Conversiones por estado (dashboard)
- Tiempo promedio de procesamiento
- Tasa de error en conversiones
- Reintentos del background processor

## 💰 Costos (AWS)

### Lambda
- **Invocaciones:** 288/día (cada 5 min) = ~8,640/mes
- **Free Tier:** 1M invocaciones/mes → **GRATIS**
- **Memoria:** 512MB, ~30s por ejecución
- **Costo estimado:** $0 (dentro del free tier)

### EventBridge
- **Reglas:** 1 regla programada
- **Free Tier:** Primeras 1M invocaciones gratis → **GRATIS**

### Alternativas Consideradas
1. ❌ **Background job en App Runner:** Requiere framework adicional, más complejo
2. ❌ **Fargate scheduled tasks:** Más costoso para task cortos
3. ✅ **Lambda + EventBridge:** Óptimo para este caso de uso

## 🧪 Testing

### Endpoints para Probar
```bash
# 1. Crear conversión
POST http://localhost:5037/api/v1/voice-conversions

# 2. Obtener conversión
GET http://localhost:5037/api/v1/voice-conversions/{id}

# 3. Webhook (interno)
POST http://localhost:5037/api/v1/voice-conversions/webhooks/conversion-result
```

Ver `.ai_doc/v1/voice_conversions.md` para ejemplos completos con curl.

## ✨ Features Implementadas

- ✅ Creación de conversiones con validación de preprocessing
- ✅ Queue automático cuando preprocessing está listo
- ✅ Background processor con reintentos automáticos
- ✅ Webhook para recibir resultados del servicio externo
- ✅ Pre-signed URLs para descargar audio convertido (12h)
- ✅ User ownership y seguridad
- ✅ Admin-only fields
- ✅ Soft delete
- ✅ Auditoría automática (timestamps)
- ✅ Índices optimizados para queries
- ✅ Logging completo
- ✅ Documentación de API

## 🎯 Próximos Pasos Sugeridos

1. **Testing:** Crear unit tests para VoiceConversionService
2. **Integration Tests:** Probar flujo completo con preprocessing
3. **CloudWatch Dashboard:** Crear dashboard con métricas clave
4. **Alertas:** Configurar alarmas para:
   - Conversiones fallidas > threshold
   - Lambda errors
   - Queue depth excesivo
5. **Rate Limiting:** Considerar límite de conversiones por usuario/período
6. **Bulk Operations:** Endpoint para crear múltiples conversiones
7. **List Endpoint:** GET /voice-conversions con paginación y filtros

## 📝 Notas Técnicas

- Enum values se almacenan como strings en DB (fácil debugging)
- Transposition se envía como integer al servicio externo
- Request ID es el GUID de la conversión como string
- Output S3 URI incluye conversion ID para unicidad
- Background processor usa advisory locks de PostgreSQL (via EF Core)
- Lambda tiene timeout de 5 minutos (suficiente para procesar batch)
- Pre-signed URLs tienen lifetime de 12 horas
- Webhook requiere request_id para correlación (campo obligatorio)
