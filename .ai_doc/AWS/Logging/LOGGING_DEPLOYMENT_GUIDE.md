# Guía de Despliegue - Logging Mejorado en AWS App Runner

## 🚀 Pasos para Desplegar las Mejoras de Logging

### 1. Pre-Despliegue (Verificaciones Locales)

#### ✅ Compilación
```bash
cd VoiceByAuribus.API
dotnet restore
dotnet build
```

#### ✅ Verificar formato de logs en desarrollo
```bash
dotnet run
```

Deberías ver logs en formato legible:
```
[10:23:45 INF] Starting VoiceByAuribus API
[10:23:46 INF] Environment: Development
[10:23:47 INF] WebApplicationBuilder created
```

#### ✅ Probar formato JSON (simular producción)
Temporalmente cambiar `appsettings.Development.json` para usar CompactJsonFormatter:
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

Ejecutar y verificar:
```bash
dotnet run
```

Deberías ver logs en JSON:
```json
{"@t":"2025-11-17T10:23:45.1234567Z","@mt":"Starting VoiceByAuribus API","@l":"Information"}
```

**Importante:** Revertir cambio después de la prueba.

---

### 2. Commit y Push

```bash
git add .
git commit -m "feat: implement structured logging with Serilog for CloudWatch

- Add Serilog with JSON formatting for production
- Add structured logging to all services (AudioFile, Preprocessing, VoiceModel)
- Add request/response logging with correlation IDs
- Add comprehensive logging to controllers
- Migrate Program.cs from Console.WriteLine to Serilog
- Add rich context to exception logging (TraceId, RequestPath, etc)
- Create CloudWatch logging documentation and examples"

git push origin main
```

---

### 3. Despliegue en AWS App Runner

#### Opción A: Auto-deploy (si está configurado)
AWS App Runner detectará el push y comenzará el despliegue automáticamente.

#### Opción B: Deploy manual
```bash
# Desde la raíz del proyecto
./scripts/deploy-to-aws.sh
```

O desde la consola de AWS:
1. Ir a AWS App Runner
2. Seleccionar el servicio `voice-by-auribus-api`
3. Click en "Deploy"

---

### 4. Verificación Post-Despliegue

#### ✅ Verificar logs de startup
1. Ir a AWS CloudWatch
2. Log Groups → `/aws/apprunner/voice-by-auribus-api/.../service`
3. Buscar logs recientes

Deberías ver (en formato JSON):
```json
{"@t":"2025-11-17T...", "@mt":"Starting VoiceByAuribus API", "@l":"Information"}
{"@t":"2025-11-17T...", "@mt":"Loading features...", "@l":"Information"}
{"@t":"2025-11-17T...", "@mt":"Application starting, listening on configured ports...", "@l":"Information"}
```

#### ✅ Probar un endpoint
```bash
curl -X GET https://your-app-runner-url.amazonaws.com/api/v1/auth/status \
  -H "Authorization: Bearer YOUR_TOKEN"
```

Verificar en CloudWatch que aparece el log:
```json
{
  "@t": "2025-11-17T...",
  "@mt": "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms",
  "RequestMethod": "GET",
  "RequestPath": "/api/v1/auth/status",
  "StatusCode": 200,
  "Elapsed": 45.67
}
```

#### ✅ Verificar campos estructurados
Ejecutar en CloudWatch Logs Insights:
```cloudwatch
fields @timestamp, RequestMethod, RequestPath, StatusCode, Elapsed
| sort @timestamp desc
| limit 10
```

Deberías ver todos los campos parseados correctamente.

---

### 5. Configuración de CloudWatch (Una sola vez)

#### A. Crear Grupo de Logs (si no existe)
```bash
aws logs create-log-group \
  --log-group-name /aws/apprunner/voice-by-auribus-api/production \
  --region us-east-1
```

#### B. Configurar Retención
```bash
aws logs put-retention-policy \
  --log-group-name /aws/apprunner/voice-by-auribus-api/production \
  --retention-in-days 30 \
  --region us-east-1
```

#### C. Crear Métricas Personalizadas

**Métrica: Error Rate**
```bash
aws logs put-metric-filter \
  --log-group-name /aws/apprunner/voice-by-auribus-api/production \
  --filter-name ErrorRate \
  --filter-pattern '{ $.@l = "Error" }' \
  --metric-transformations \
      metricName=ErrorCount,metricNamespace=VoiceByAuribus/API,metricValue=1 \
  --region us-east-1
```

**Métrica: Request Count**
```bash
aws logs put-metric-filter \
  --log-group-name /aws/apprunner/voice-by-auribus-api/production \
  --filter-name RequestCount \
  --filter-pattern '{ $.RequestMethod = * }' \
  --metric-transformations \
      metricName=RequestCount,metricNamespace=VoiceByAuribus/API,metricValue=1 \
  --region us-east-1
```

**Métrica: Slow Requests (> 2s)**
```bash
aws logs put-metric-filter \
  --log-group-name /aws/apprunner/voice-by-auribus-api/production \
  --filter-name SlowRequests \
  --filter-pattern '{ $.Elapsed > 2000 }' \
  --metric-transformations \
      metricName=SlowRequestCount,metricNamespace=VoiceByAuribus/API,metricValue=1 \
  --region us-east-1
```

---

### 6. Configuración de Alarmas

#### Alarma: Alta Tasa de Errores
```bash
aws cloudwatch put-metric-alarm \
  --alarm-name voice-api-high-error-rate \
  --alarm-description "Alert when error rate is high" \
  --metric-name ErrorCount \
  --namespace VoiceByAuribus/API \
  --statistic Sum \
  --period 300 \
  --evaluation-periods 2 \
  --threshold 10 \
  --comparison-operator GreaterThanThreshold \
  --alarm-actions arn:aws:sns:us-east-1:YOUR_ACCOUNT:alerts \
  --region us-east-1
```

#### Alarma: Muchas Requests Lentas
```bash
aws cloudwatch put-metric-alarm \
  --alarm-name voice-api-slow-requests \
  --alarm-description "Alert when too many slow requests" \
  --metric-name SlowRequestCount \
  --namespace VoiceByAuribus/API \
  --statistic Sum \
  --period 300 \
  --evaluation-periods 2 \
  --threshold 5 \
  --comparison-operator GreaterThanThreshold \
  --alarm-actions arn:aws:sns:us-east-1:YOUR_ACCOUNT:alerts \
  --region us-east-1
```

---

### 7. Crear Dashboard de CloudWatch

#### Via AWS Console:
1. Ir a CloudWatch → Dashboards
2. Create Dashboard → "VoiceByAuribus-API-Production"
3. Agregar widgets:

**Widget 1: Error Count (Line)**
```json
{
  "metrics": [
    [ "VoiceByAuribus/API", "ErrorCount", { "stat": "Sum" } ]
  ],
  "period": 300,
  "region": "us-east-1",
  "title": "Error Count (5min intervals)"
}
```

**Widget 2: Request Count (Line)**
```json
{
  "metrics": [
    [ "VoiceByAuribus/API", "RequestCount", { "stat": "Sum" } ]
  ],
  "period": 300,
  "region": "us-east-1",
  "title": "Request Count (5min intervals)"
}
```

**Widget 3: Recent Errors (Logs)**
```cloudwatch
fields @timestamp, @message, ExceptionType, RequestPath
| filter @level = "Error"
| sort @timestamp desc
| limit 20
```

**Widget 4: P95 Latency (Line)**
```cloudwatch
fields Elapsed
| stats percentile(Elapsed, 95) as P95 by bin(@timestamp, 5m)
```

---

### 8. Testing en Producción

#### Test 1: Crear Audio File
```bash
curl -X POST https://your-app-runner-url.amazonaws.com/api/v1/audio-files \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "file_name": "test.mp3",
    "mime_type": "audio/mpeg"
  }'
```

**Verificar en CloudWatch:**
```cloudwatch
fields @timestamp, @message, AudioFileId, FileName, UserId
| filter @message like /Creating audio file/
| sort @timestamp desc
| limit 1
```

#### Test 2: Simular Error (endpoint no autorizado)
```bash
curl -X GET https://your-app-runner-url.amazonaws.com/api/v1/audio-files/invalid-id
```

**Verificar en CloudWatch:**
```cloudwatch
fields @timestamp, @message, ExceptionType, RequestPath
| filter @level = "Error"
| sort @timestamp desc
| limit 1
```

---

## 🔄 Rollback Plan

Si algo sale mal después del despliegue:

### Opción 1: Rollback de App Runner
1. Ir a AWS App Runner
2. Seleccionar el servicio
3. Deployments → Ver historial
4. Seleccionar deployment anterior
5. Click "Rollback"

### Opción 2: Rollback de Git
```bash
git revert HEAD
git push origin main
```
App Runner desplegará automáticamente la versión anterior.

### Opción 3: Variables de Entorno (Emergency)
Si solo necesitas cambiar el nivel de log:
1. App Runner → Configuration
2. Environment variables
3. Agregar: `Serilog__MinimumLevel__Default = Warning`
4. Redeploy

---

## 📊 Monitoreo Post-Despliegue (Primeras 24h)

### Checklist:
- [ ] Verificar que los logs aparecen en formato JSON
- [ ] Confirmar que todos los campos están parseados (RequestMethod, StatusCode, etc)
- [ ] Verificar que no hay aumento en tasa de errores
- [ ] Comprobar que el rendimiento no se degradó
- [ ] Probar queries de CloudWatch Logs Insights
- [ ] Verificar que las alarmas están funcionando
- [ ] Revisar dashboard de CloudWatch

### Queries de Monitoreo:

**1. Volumen de Logs**
```cloudwatch
fields @timestamp
| stats count() as LogCount by bin(@timestamp, 5m)
```

**2. Errores Nuevos**
```cloudwatch
fields @timestamp, @message, ExceptionType
| filter @level = "Error"
| sort @timestamp desc
| limit 50
```

**3. Rendimiento**
```cloudwatch
fields Elapsed
| stats 
    avg(Elapsed) as Avg,
    percentile(Elapsed, 95) as P95,
    percentile(Elapsed, 99) as P99
```

---

## 🎓 Capacitación del Equipo

### Para Desarrolladores:
1. Leer `.ai_doc/LOGGING_CLOUDWATCH.md`
2. Revisar ejemplos en `.ai_doc/LOGGING_CLOUDWATCH_EXAMPLES.md`
3. Practicar con queries de CloudWatch Logs Insights

### Para Operaciones:
1. Configurar alarmas personalizadas según necesidades
2. Crear dashboards adicionales para monitoreo específico
3. Establecer procedimientos de respuesta a incidentes

---

## 📝 Notas Importantes

1. **Costo de CloudWatch:**
   - Logs Insights: $0.005 por GB escaneado
   - Retención: Gratis hasta 5 GB/mes
   - Monitorear uso mensual

2. **Rendimiento:**
   - Serilog es muy eficiente
   - El impacto en rendimiento es mínimo (<1ms por request)
   - JSON compacto reduce tamaño de logs en ~30%

3. **Seguridad:**
   - Logs no contienen información sensible
   - Todos los IDs son UUID no reversibles
   - S3 URIs no exponen datos privados

4. **Escalabilidad:**
   - Sistema preparado para alto volumen
   - CloudWatch maneja millones de logs/día
   - Queries optimizadas con índices

---

## ✅ Checklist Final de Despliegue

- [ ] Código compilado y testeado localmente
- [ ] Commit y push a Git
- [ ] Despliegue en App Runner exitoso
- [ ] Logs aparecen en CloudWatch en formato JSON
- [ ] Queries de CloudWatch funcionan correctamente
- [ ] Métricas personalizadas configuradas
- [ ] Alarmas configuradas y testeadas
- [ ] Dashboard creado
- [ ] Documentación revisada por el equipo
- [ ] Plan de rollback comunicado
- [ ] Monitoreo activo durante primeras 24h

---

## 🆘 Contactos de Emergencia

**Si hay problemas después del despliegue:**

1. **Verificar estado del servicio:**
   - AWS App Runner Console
   - CloudWatch Logs

2. **Rollback inmediato si:**
   - Tasa de errores > 10%
   - Servicio no responde
   - Logs no aparecen en CloudWatch

3. **Escalar a:**
   - Tech Lead (problemas de código)
   - DevOps (problemas de infraestructura)
   - AWS Support (problemas de CloudWatch)

---

## 🎉 Éxito del Despliegue

Una vez completado, tendrás:

✅ Logs estructurados en JSON optimizados para CloudWatch
✅ Queries rápidas y eficientes con Logs Insights
✅ Dashboards para monitoreo en tiempo real
✅ Alarmas automáticas para problemas críticos
✅ Mejor debugging y troubleshooting
✅ Auditoría completa de operaciones
✅ Base para métricas de negocio y KPIs
