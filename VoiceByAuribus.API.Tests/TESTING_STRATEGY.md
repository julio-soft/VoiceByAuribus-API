# VoiceByAuribus API - Estrategia de Testing

## 📋 Resumen Ejecutivo

Este documento define la estrategia completa de testing para VoiceByAuribus API, un sistema de conversión de voz que sigue una arquitectura de **Vertical Slice + Clean Architecture** con múltiples capas de complejidad que requieren diferentes tipos de pruebas.

### Enfoque Recomendado: Pirámide de Testing Moderna

```
       /\
      /  \     E2E Tests (5-10%)
     /----\    - Flujos críticos completos
    /      \   - WebApplicationFactory
   /--------\  
  / Integration\ Integration Tests (30-40%)
 /    Tests     \ - Controllers + Services + DB
/--------------\ - TestContainers (PostgreSQL)
|              |
|  Unit Tests  | Unit Tests (50-60%)
|  (Foundation)| - Servicios, Validators, Mappers
|              | - Helpers, Encriptación
|______________| - Mocks con Moq/NSubstitute

```

## 🎯 Análisis del Proyecto

### Arquitectura Identificada

**VoiceByAuribus.API** utiliza:
- **Vertical Slice Architecture**: Cada feature es autónoma
- **Clean Architecture**: Separación en capas (Domain, Application, Presentation)
- **Features principales**:
  - ✅ **Auth**: Cognito M2M authentication (scopes, policies)
  - ✅ **Voices**: Voice models management
  - ✅ **AudioFiles**: Upload with S3 pre-signed URLs + preprocessing
  - ✅ **VoiceConversions**: Async processing con SQS + background services
  - ✅ **WebhookSubscriptions**: Webhook delivery system con encriptación AES-256-GCM

### Componentes Críticos para Testing

#### 1. **Domain Models** (6 entidades principales)
- `VoiceModel`, `AudioFile`, `AudioPreprocessing`
- `VoiceConversion` (con optimistic locking via `RowVersion`)
- `WebhookSubscription`, `WebhookDeliveryLog` (con optimistic locking)

#### 2. **Services** (Alta complejidad de negocio)
- `VoiceConversionService`: Estado management, retry logic, SQS integration
- `WebhookSubscriptionService`: CRUD + validación de límites (5 max)
- `WebhookDeliveryService`: HTTP delivery + HMAC-SHA256 signing
- `AudioFileService`: S3 pre-signed URLs + validation
- `EncryptionService`: AES-256-GCM encryption/decryption
- `CurrentUserService`: JWT claims extraction

#### 3. **Background Services** (Procesos críticos asincrónicos)
- `VoiceConversionProcessorService`: Polling cada 3s + optimistic locking
- `WebhookDeliveryProcessorService`: Batch processing + retry exponential backoff

#### 4. **Controllers** (8 controllers)
- Auth, Voices, AudioFiles, VoiceConversions, WebhookSubscriptions, Health
- Webhooks (upload notification, preprocessing result, test endpoint)

#### 5. **Infrastructure**
- `ApplicationDbContext`: Global filters (soft-delete, user isolation), auditing
- `GlobalExceptionHandlerMiddleware`: Centralized error handling
- `ValidationFilter`: FluentValidation integration
- `WebhookAuthenticationAttribute`: API key validation

## 📊 Tipos de Tests Recomendados

### 1. **Unit Tests** (Prioridad: ALTA - 50-60% cobertura)

**Objetivo**: Probar lógica de negocio aislada sin dependencias externas.

**Componentes a testear**:

#### A. **Services (Con Mocks)**
```csharp
// Ejemplo: VoiceConversionService
- ✅ CreateVoiceConversionAsync
  - Validación de AudioFile ownership
  - Validación de VoiceModel existence
  - Pitch shift conversion (PitchShiftHelper)
  - Status flow: PendingPreprocessing → Queued
  - Webhook event publishing (mocked)
  
- ✅ ProcessPendingConversionsAsync
  - Batch processing con optimistic locking
  - Retry logic con exponential backoff
  - SQS queue selection (preview vs full)
  - Error handling y status transitions
  
- ✅ CompleteVoiceConversionAsync (webhook callback)
  - Status transitions: Processing → Completed
  - Webhook event publishing
  - Output S3 URI validation
```

#### B. **Encryption & Security**
```csharp
// EncryptionService
- ✅ Encrypt/Decrypt round-trip
- ✅ Invalid key handling
- ✅ Corrupted data handling
- ✅ Format validation

// WebhookSecretService
- ✅ GenerateSecret (64 char hex)
- ✅ Encrypt/Decrypt secrets
- ✅ HMAC-SHA256 signature generation
- ✅ Signature verification
```

#### C. **Helpers & Utilities**
```csharp
// PitchShiftHelper
- ✅ ToTransposition: "same_octave" → Transposition.SameOctave
- ✅ ToPitchShiftString: Transposition.LowerOctave → "lower_octave"
- ✅ Invalid pitch shift handling

// SqsQueueResolver
- ✅ Queue name → URL resolution
- ✅ Caching behavior
- ✅ Missing queue handling
```

#### D. **Validators**
```csharp
// FluentValidation
- ✅ CreateVoiceConversionDtoValidator
  - AudioFileId required
  - VoiceModelId required
  - PitchShift enum validation
  - UsePreview boolean
```

#### E. **Mappers**
```csharp
// Static mapper methods
- ✅ AudioFileMapper.MapToResponseDto (con isAdmin flag)
- ✅ VoiceConversionMapper.MapToResponseDto (pitch_shift abstraction)
- ✅ WebhookSubscriptionMappers.ToResponseDto (extension methods)
```

**Herramientas**:
- **xUnit**: Framework de testing moderno
- **Moq** o **NSubstitute**: Mocking de dependencias (DbContext, ILogger, etc.)
- **FluentAssertions**: Assertions legibles
- **AutoFixture**: Generación de datos de prueba

### 2. **Integration Tests** (Prioridad: ALTA - 30-40% cobertura)

**Objetivo**: Probar interacciones entre capas (Controllers → Services → DB) con infraestructura real.

**Componentes a testear**:

#### A. **Controllers + Services + Database**
```csharp
// Flujos completos por feature
- ✅ AudioFiles Flow:
  1. POST /audio-files → Create record + pre-signed URL
  2. Simular upload S3 (webhook notification)
  3. GET /audio-files/{id} → Verificar status
  4. Preprocessing webhook callback
  
- ✅ VoiceConversions Flow:
  1. POST /voice-conversions → Create conversion
  2. Background processor simulation
  3. GET /voice-conversions/{id} → Check status
  4. Completion webhook callback
  
- ✅ WebhookSubscriptions Flow:
  1. POST /webhook-subscriptions → Create (max 5 limit)
  2. GET /webhook-subscriptions → List user's subscriptions
  3. POST /{id}/test → Test webhook delivery
  4. PATCH /{id} → Update subscription
  5. DELETE /{id} → Soft delete
```

#### B. **Database Integration**
```csharp
// ApplicationDbContext tests
- ✅ Global filters (soft-delete, user isolation)
- ✅ Auditing (CreatedAt, UpdatedAt automatic)
- ✅ User ownership (IHasUserId auto-assignment)
- ✅ Optimistic locking (VoiceConversion, WebhookDeliveryLog)
- ✅ Entity configurations (enums → string conversion)
```

#### C. **Background Services**
```csharp
// VoiceConversionProcessorService
- ✅ Process pending conversions in batches
- ✅ Optimistic locking prevents race conditions
- ✅ Retry logic con MaxRetryAttempts
- ✅ Health check status reporting

// WebhookDeliveryProcessorService
- ✅ Batch processing (20 webhooks max)
- ✅ Exponential backoff (2^attempt seconds)
- ✅ Stuck webhook recovery (5 minutes timeout)
- ✅ Auto-disable subscriptions (10 consecutive failures)
```

**Herramientas**:
- **WebApplicationFactory<Program>**: Test server in-memory
- **Testcontainers**: PostgreSQL real container (mejor que in-memory)
- **Respawn**: Database cleanup entre tests
- **WireMock.Net**: Mock external APIs (SQS, S3)

### 3. **End-to-End (E2E) Tests** (Prioridad: MEDIA - 5-10% cobertura)

**Objetivo**: Probar flujos de usuario completos desde HTTP request hasta respuesta final.

**Flujos críticos**:

#### A. **Voice Conversion Flow (Usuario completo)**
```
1. Usuario sube audio → POST /audio-files
2. Usuario obtiene pre-signed URL
3. Simular upload S3 (webhook notification)
4. Esperar preprocessing → webhook callback
5. Usuario crea conversion → POST /voice-conversions
6. Background processor procesa → SQS
7. External service callback → POST /webhooks/voice-inference-result
8. Usuario obtiene resultado → GET /voice-conversions/{id}
```

#### B. **Webhook Delivery Flow**
```
1. Usuario crea webhook subscription → POST /webhook-subscriptions
2. Usuario recibe plain secret (solo una vez)
3. Sistema procesa conversion → Trigger webhook event
4. Background processor entrega webhook → HTTP POST with HMAC
5. Cliente verifica firma HMAC-SHA256
6. Usuario consulta delivery logs → GET /webhook-subscriptions/{id}/deliveries
```

**Herramientas**:
- **WebApplicationFactory**: Servidor de prueba
- **Testcontainers**: PostgreSQL + LocalStack (S3/SQS local)
- **HTTP Client**: Simular cliente externo

## 🏗️ Estructura del Proyecto de Tests

### Organización Propuesta

```
VoiceByAuribus.API.Tests/
├── VoiceByAuribus.API.Tests.csproj
│
├── Unit/                                    # Tests unitarios (aislados)
│   ├── Features/
│   │   ├── AudioFiles/
│   │   │   ├── Services/
│   │   │   │   └── AudioFileServiceTests.cs
│   │   │   └── Mappers/
│   │   │       └── AudioFileMapperTests.cs
│   │   ├── VoiceConversions/
│   │   │   ├── Services/
│   │   │   │   └── VoiceConversionServiceTests.cs
│   │   │   ├── Validators/
│   │   │   │   └── CreateVoiceConversionDtoValidatorTests.cs
│   │   │   └── Helpers/
│   │   │       └── PitchShiftHelperTests.cs
│   │   ├── WebhookSubscriptions/
│   │   │   ├── Services/
│   │   │   │   ├── WebhookSubscriptionServiceTests.cs
│   │   │   │   ├── WebhookSecretServiceTests.cs
│   │   │   │   └── WebhookDeliveryServiceTests.cs
│   │   │   └── Mappers/
│   │   │       └── WebhookSubscriptionMappersTests.cs
│   │   └── Voices/
│   │       └── Services/
│   │           └── VoiceModelServiceTests.cs
│   │
│   └── Shared/
│       ├── Services/
│       │   ├── EncryptionServiceTests.cs
│       │   ├── CurrentUserServiceTests.cs
│       │   └── SqsQueueResolverTests.cs
│       └── Middleware/
│           └── GlobalExceptionHandlerMiddlewareTests.cs
│
├── Integration/                             # Tests de integración (DB + Services)
│   ├── Features/
│   │   ├── AudioFiles/
│   │   │   ├── AudioFilesControllerTests.cs
│   │   │   └── AudioFilesFlowTests.cs
│   │   ├── VoiceConversions/
│   │   │   ├── VoiceConversionsControllerTests.cs
│   │   │   ├── VoiceConversionProcessorTests.cs
│   │   │   └── VoiceConversionsFlowTests.cs
│   │   └── WebhookSubscriptions/
│   │       ├── WebhookSubscriptionsControllerTests.cs
│   │       ├── WebhookDeliveryProcessorTests.cs
│   │       └── WebhookDeliveryFlowTests.cs
│   │
│   └── Infrastructure/
│       ├── Data/
│       │   ├── ApplicationDbContextTests.cs
│       │   └── GlobalFiltersTests.cs
│       └── Security/
│           └── AuthenticationTests.cs
│
├── E2E/                                     # Tests end-to-end (flujos completos)
│   ├── VoiceConversionE2ETests.cs
│   └── WebhookDeliveryE2ETests.cs
│
└── Helpers/                                 # Utilities para tests
    ├── TestWebApplicationFactory.cs         # Factory customizado
    ├── DatabaseFixture.cs                   # Testcontainers PostgreSQL
    ├── MockServices/
    │   ├── MockSqsService.cs
    │   ├── MockS3Service.cs
    │   └── MockWebhookClient.cs
    └── Builders/                            # Test data builders
        ├── AudioFileBuilder.cs
        ├── VoiceConversionBuilder.cs
        └── WebhookSubscriptionBuilder.cs
```

## 📝 Plan de Implementación por Fases

### **FASE 1: Configuración Inicial** (1-2 días)
**Objetivo**: Crear infraestructura base de testing.

**Tareas**:
1. ✅ Crear proyecto `VoiceByAuribus.API.Tests`
   ```bash
   dotnet new xunit -n VoiceByAuribus.API.Tests
   dotnet sln add VoiceByAuribus.API.Tests/VoiceByAuribus.API.Tests.csproj
   ```

2. ✅ Instalar paquetes NuGet:
   ```xml
   <PackageReference Include="xUnit" Version="2.9.2" />
   <PackageReference Include="xunit.runner.visualstudio" Version="3.0.0" />
   <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
   
   <!-- Mocking -->
   <PackageReference Include="Moq" Version="4.20.72" />
   <PackageReference Include="NSubstitute" Version="5.3.0" />
   
   <!-- Assertions -->
   <PackageReference Include="FluentAssertions" Version="7.0.0" />
   
   <!-- Integration Tests -->
   <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0" />
   <PackageReference Include="Testcontainers.PostgreSql" Version="3.11.0" />
   <PackageReference Include="Respawn" Version="6.2.1" />
   
   <!-- Test Data -->
   <PackageReference Include="AutoFixture" Version="4.18.1" />
   <PackageReference Include="Bogus" Version="35.6.1" />
   
   <!-- Mock External APIs -->
   <PackageReference Include="WireMock.Net" Version="1.6.8" />
   ```

3. ✅ Crear estructura de carpetas (Unit, Integration, E2E, Helpers)

4. ✅ Crear helpers base:
   - `TestWebApplicationFactory.cs`
   - `DatabaseFixture.cs` (con Testcontainers)
   - `MockAuthenticationHandler.cs`

**Criterio de completitud**: Proyecto compila, ejecuta test dummy, Testcontainers funciona.

---

### **FASE 2: Unit Tests - Shared Services** (2-3 días)
**Objetivo**: Testear servicios compartidos críticos.

**Prioridad**: ⭐⭐⭐ CRÍTICO

**Tareas**:
1. ✅ `EncryptionServiceTests.cs`
   - Encrypt/Decrypt round-trip
   - Invalid master key handling
   - Corrupted data handling
   - Format validation (nonce:ciphertext:tag)

2. ✅ `CurrentUserServiceTests.cs`
   - Extract UserId from JWT claims
   - Extract Scopes (comma-separated)
   - IsAdmin flag detection
   - Anonymous user handling

3. ✅ `SqsQueueResolverTests.cs`
   - Queue name → URL resolution
   - Caching behavior (second call no AWS)
   - Missing queue exception

4. ✅ `GlobalExceptionHandlerMiddlewareTests.cs`
   - UnauthorizedAccessException → 401
   - ArgumentException → 400
   - KeyNotFoundException → 404
   - Generic exceptions → 500

**Criterio de completitud**: 100% cobertura de Shared services críticos.

---

### **FASE 3: Unit Tests - WebhookSubscriptions Feature** (3-4 días)
**Objetivo**: Testear lógica de webhooks (más compleja del sistema).

**Prioridad**: ⭐⭐⭐ CRÍTICO

**Tareas**:
1. ✅ `WebhookSecretServiceTests.cs`
   - GenerateSecret returns 64-char hex
   - EncryptSecret/DecryptSecret round-trip
   - GenerateSignature produces sha256={hex}
   - VerifySignature validates HMAC correctly
   - Invalid signature detection

2. ✅ `WebhookSubscriptionServiceTests.cs`
   - CreateSubscription generates encrypted secret
   - Max 5 subscriptions limit enforced
   - GetUserSubscriptions filters by userId
   - UpdateSubscription preserves secret
   - DeleteSubscription soft-deletes

3. ✅ `WebhookDeliveryServiceTests.cs`
   - DeliverWebhookAsync sends HTTP POST
   - HMAC signature in X-Webhook-Signature header
   - Timestamp validation
   - 2xx response → Delivered status
   - 4xx/5xx → Failed status
   - Network errors → Failed status

4. ✅ `WebhookSubscriptionMappersTests.cs`
   - ToResponseDto maps all fields
   - ToCreatedResponseDto includes plain secret
   - Extension methods work correctly

**Criterio de completitud**: Webhooks feature 90%+ cobertura, todos los edge cases cubiertos.

---

### **FASE 4: Unit Tests - VoiceConversions Feature** (3-4 días)
**Objetivo**: Testear lógica de conversiones (core business logic).

**Prioridad**: ⭐⭐⭐ CRÍTICO

**Tareas**:
1. ✅ `PitchShiftHelperTests.cs`
   - ToTransposition: all 7 valid values
   - ToPitchShiftString: enum → string
   - Invalid pitch shift → ArgumentException

2. ✅ `CreateVoiceConversionDtoValidatorTests.cs`
   - AudioFileId required
   - VoiceModelId required
   - PitchShift valid enum
   - UsePreview boolean

3. ✅ `VoiceConversionServiceTests.cs`
   - CreateVoiceConversionAsync:
     - AudioFile ownership validation
     - VoiceModel existence check
     - Status = PendingPreprocessing (preprocessing not done)
     - Status = Queued (preprocessing done, SQS enqueued)
     - Webhook event published (mocked)
   
   - ProcessPendingConversionsAsync:
     - Batch processing (multiple conversions)
     - Optimistic locking (DbUpdateConcurrencyException)
     - Retry logic (max 5 attempts)
     - SQS queue selection (UsePreview → PreviewQueue)
   
   - CompleteVoiceConversionAsync:
     - Status transition: Processing → Completed
     - OutputS3Uri set
     - Webhook event published

**Criterio de completitud**: VoiceConversions feature 85%+ cobertura.

---

### **FASE 5: Unit Tests - AudioFiles & Voices** (2-3 días)
**Objetivo**: Completar unit tests de features restantes.

**Prioridad**: ⭐⭐ IMPORTANTE

**Tareas**:
1. ✅ `AudioFileServiceTests.cs`
   - CreateAudioFileAsync generates S3 URI
   - Pre-signed URL generation
   - RegenerateUploadUrlAsync (only AwaitingUpload)
   - GetAudioFileByIdAsync (user ownership)
   - Mappers test (admin vs non-admin data)

2. ✅ `VoiceModelServiceTests.cs`
   - GetAllVoiceModelsAsync
   - GetVoiceModelByIdAsync
   - Admin-only fields (VoiceModelPath, VoiceModelIndexPath)
   - Pre-signed URL generation

**Criterio de completitud**: AudioFiles + Voices features 80%+ cobertura.

---

### **FASE 6: Integration Tests - Controllers + DB** (4-5 días)
**Objetivo**: Testear interacciones HTTP → Services → DB.

**Prioridad**: ⭐⭐⭐ CRÍTICO

**Tareas**:
1. ✅ Configurar `TestWebApplicationFactory`:
   - Testcontainers PostgreSQL
   - Mock authentication (fake JWT)
   - Mock AWS services (S3, SQS)

2. ✅ `AudioFilesControllerTests.cs`:
   - POST /audio-files → 201 Created
   - GET /audio-files/{id} → 200 OK (user ownership)
   - GET /audio-files/{id} → 404 Not Found (otro user)
   - POST /audio-files/{id}/regenerate-upload-url

3. ✅ `VoiceConversionsControllerTests.cs`:
   - POST /voice-conversions → 201 Created
   - GET /voice-conversions → 200 OK (paginated)
   - GET /voice-conversions/{id} → 200 OK
   - GET /voice-conversions/{id} → 404 Not Found

4. ✅ `WebhookSubscriptionsControllerTests.cs`:
   - POST /webhook-subscriptions → 201 Created
   - GET /webhook-subscriptions → 200 OK
   - PATCH /webhook-subscriptions/{id} → 200 OK
   - DELETE /webhook-subscriptions/{id} → 204 No Content
   - POST /webhook-subscriptions/{id}/test → 200 OK
   - POST /webhook-subscriptions/{id}/regenerate-secret → 200 OK
   - GET /webhook-subscriptions/{id}/deliveries → 200 OK

5. ✅ `ApplicationDbContextTests.cs`:
   - Global filters (soft-delete, user isolation)
   - Auditing (CreatedAt, UpdatedAt)
   - User ownership (IHasUserId)
   - Optimistic locking (RowVersion)

**Criterio de completitud**: Todos los controllers HTTP 200/201/204/400/404 testeados.

---

### **FASE 7: Integration Tests - Background Services** (3-4 días)
**Objetivo**: Testear procesamiento asíncrono (crítico para producción).

**Prioridad**: ⭐⭐⭐ CRÍTICO

**Tareas**:
1. ✅ `VoiceConversionProcessorTests.cs`:
   - ProcessPendingConversionsAsync procesa batch
   - Optimistic locking previene race conditions
   - Retry logic funciona (max 5 attempts)
   - Health check status reporting
   - Timeout handling (40 segundos)

2. ✅ `WebhookDeliveryProcessorTests.cs`:
   - ProcessPendingDeliveriesAsync procesa batch (20 max)
   - Optimistic locking previene duplicados
   - Exponential backoff (2^attempt seconds)
   - Stuck webhook recovery (5 minutes)
   - Auto-disable subscriptions (10 failures)
   - Test endpoint (fire-and-forget, no database)

**Criterio de completitud**: Background services 85%+ cobertura, race conditions probadas.

---

### **FASE 8: E2E Tests - Critical Flows** (2-3 días)
**Objetivo**: Validar flujos completos de usuario.

**Prioridad**: ⭐⭐ IMPORTANTE

**Tareas**:
1. ✅ `VoiceConversionE2ETests.cs`:
   - Usuario sube audio → preprocessing → conversion → resultado
   - Simular webhooks (S3 notification, preprocessing callback)
   - Verificar status transitions
   - Verificar pre-signed URLs generadas

2. ✅ `WebhookDeliveryE2ETests.cs`:
   - Usuario crea subscription
   - Sistema entrega webhook con HMAC
   - Cliente verifica firma
   - Sistema maneja reintentos
   - Sistema auto-desactiva después de 10 failures

**Criterio de completitud**: Flujos críticos end-to-end pasan.

---

### **FASE 9: Performance & Load Tests** (OPCIONAL - 2-3 días)
**Objetivo**: Validar rendimiento bajo carga.

**Prioridad**: ⭐ OPCIONAL (post-MVP)

**Tareas**:
1. ✅ Load test: 100 conversiones concurrentes
2. ✅ Load test: 1000 webhooks/minuto
3. ✅ Stress test: Background processors con 10 instancias paralelas
4. ✅ Benchmark: Encryption/Decryption performance

**Herramientas**: NBomber, BenchmarkDotNet

---

### **FASE 10: CI/CD Integration** (1-2 días)
**Objetivo**: Automatizar tests en pipeline.

**Prioridad**: ⭐⭐⭐ CRÍTICO (producción)

**Tareas**:
1. ✅ Configurar GitHub Actions:
   ```yaml
   - Run unit tests
   - Run integration tests (Testcontainers)
   - Generate coverage report (Coverlet)
   - Upload to CodeCov
   ```

2. ✅ Quality gates:
   - Mínimo 70% cobertura global
   - Todos los tests deben pasar
   - No warnings de seguridad

**Criterio de completitud**: Pipeline ejecuta tests automáticamente en PRs.

---

## 🛠️ Herramientas y Paquetes Recomendados

### Testing Frameworks
```xml
<!-- Test Framework -->
<PackageReference Include="xUnit" Version="2.9.2" />
<PackageReference Include="xunit.runner.visualstudio" Version="3.0.0" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
```

### Mocking & Assertions
```xml
<!-- Mocking (elegir uno) -->
<PackageReference Include="Moq" Version="4.20.72" />
<PackageReference Include="NSubstitute" Version="5.3.0" />

<!-- Assertions -->
<PackageReference Include="FluentAssertions" Version="7.0.0" />
```

### Integration Tests
```xml
<!-- WebApplicationFactory -->
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0" />

<!-- Testcontainers -->
<PackageReference Include="Testcontainers" Version="3.11.0" />
<PackageReference Include="Testcontainers.PostgreSql" Version="3.11.0" />

<!-- Database cleanup -->
<PackageReference Include="Respawn" Version="6.2.1" />
```

### Test Data Generators
```xml
<PackageReference Include="AutoFixture" Version="4.18.1" />
<PackageReference Include="Bogus" Version="35.6.1" />
```

### Mocking External Services
```xml
<PackageReference Include="WireMock.Net" Version="1.6.8" />
<PackageReference Include="LocalStack.Client" Version="1.8.0" />
```

### Code Coverage
```xml
<PackageReference Include="coverlet.collector" Version="6.0.3" />
<PackageReference Include="coverlet.msbuild" Version="6.0.3" />
```

### Performance Testing (Opcional)
```xml
<PackageReference Include="BenchmarkDotNet" Version="0.14.0" />
<PackageReference Include="NBomber" Version="6.0.1" />
```

---

## 📊 Métricas de Éxito

### Cobertura de Código Objetivo
- **Unit Tests**: 80%+ en Services, Validators, Helpers
- **Integration Tests**: 70%+ en Controllers, Background Services
- **E2E Tests**: Flujos críticos (no medido en %)
- **Global**: 75%+ cobertura total

### Quality Gates
- ✅ Todos los tests pasan en CI/CD
- ✅ No regression (tests existentes no fallan)
- ✅ Tiempo de ejecución < 5 minutos (Unit + Integration)
- ✅ E2E tests < 10 minutos
- ✅ Sin warnings de seguridad

---

## 🎓 Mejores Prácticas de Testing

### 1. **Naming Conventions**
```csharp
// Patrón: MethodName_StateUnderTest_ExpectedBehavior
[Fact]
public void CreateVoiceConversionAsync_WithValidData_ReturnsCreatedConversion() { }

[Fact]
public void CreateVoiceConversionAsync_WithInvalidAudioFile_ThrowsInvalidOperationException() { }

[Fact]
public void ProcessPendingConversionsAsync_WithConcurrentUpdates_HandlesOptimisticLocking() { }
```

### 2. **Arrange-Act-Assert (AAA Pattern)**
```csharp
[Fact]
public async Task EncryptDecrypt_WithValidData_ReturnsOriginalText()
{
    // Arrange
    var service = CreateEncryptionService();
    var plainText = "test-secret-value";
    
    // Act
    var encrypted = service.Encrypt(plainText);
    var decrypted = service.Decrypt(encrypted);
    
    // Assert
    decrypted.Should().Be(plainText);
}
```

### 3. **Test Data Builders**
```csharp
public class AudioFileBuilder
{
    private Guid _userId = Guid.NewGuid();
    private string _fileName = "test.mp3";
    private UploadStatus _status = UploadStatus.Uploaded;
    
    public AudioFileBuilder WithUserId(Guid userId)
    {
        _userId = userId;
        return this;
    }
    
    public AudioFileBuilder WithUploadCompleted()
    {
        _status = UploadStatus.Uploaded;
        return this;
    }
    
    public AudioFile Build()
    {
        return new AudioFile
        {
            UserId = _userId,
            FileName = _fileName,
            UploadStatus = _status,
            // ...
        };
    }
}

// Uso:
var audioFile = new AudioFileBuilder()
    .WithUserId(userId)
    .WithUploadCompleted()
    .Build();
```

### 4. **Integration Test Base Class**
```csharp
public abstract class IntegrationTestBase : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    protected HttpClient Client { get; }
    protected ApplicationDbContext DbContext { get; }
    
    protected IntegrationTestBase(DatabaseFixture fixture)
    {
        Client = fixture.CreateClient();
        DbContext = fixture.CreateDbContext();
    }
    
    public async Task InitializeAsync()
    {
        // Seed data común
    }
    
    public async Task DisposeAsync()
    {
        // Cleanup database
        await fixture.ResetDatabaseAsync();
    }
}
```

### 5. **Mocking Best Practices**
```csharp
// ✅ CORRECTO: Mock de interfaz
var mockSqsService = new Mock<ISqsService>();
mockSqsService
    .Setup(x => x.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync("message-id-123");

// ❌ INCORRECTO: No mockear ApplicationDbContext directamente
// Usar in-memory o Testcontainers
```

---

## 📚 Recursos y Referencias

### Documentación Oficial
- [xUnit Documentation](https://xunit.net/)
- [Moq Quickstart](https://github.com/moq/moq4)
- [FluentAssertions Docs](https://fluentassertions.com/)
- [Testcontainers .NET](https://dotnet.testcontainers.org/)

### Testing Patterns
- [Microsoft - Integration Tests in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)
- [Test Pyramid Pattern](https://martinfowler.com/articles/practical-test-pyramid.html)
- [Vertical Slice Testing](https://jimmybogard.com/vertical-slice-architecture/)

### Clean Architecture Testing
- [Clean Architecture Testing Strategies](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [Testing Microservices](https://martinfowler.com/articles/microservice-testing/)

---

## 🚀 Comandos Útiles

### Ejecutar Tests
```bash
# Todos los tests
dotnet test

# Solo unit tests
dotnet test --filter "FullyQualifiedName~Unit"

# Solo integration tests
dotnet test --filter "FullyQualifiedName~Integration"

# Con cobertura
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Watch mode (re-ejecuta al cambiar código)
dotnet watch test
```

### Coverage Reports
```bash
# Generar reporte HTML
dotnet reportgenerator \
  -reports:"coverage.opencover.xml" \
  -targetdir:"coverage-report" \
  -reporttypes:Html

# Abrir reporte
open coverage-report/index.html
```

---

## ✅ Checklist Final

### Pre-Implementación
- [x] Análisis de arquitectura completado
- [x] Estrategia de testing definida
- [x] Plan de fases documentado
- [ ] Proyecto de tests creado
- [ ] Paquetes NuGet instalados
- [ ] Estructura de carpetas creada

### Durante Implementación
- [ ] Unit tests - Shared services (Fase 2)
- [ ] Unit tests - WebhookSubscriptions (Fase 3)
- [ ] Unit tests - VoiceConversions (Fase 4)
- [ ] Unit tests - AudioFiles & Voices (Fase 5)
- [ ] Integration tests - Controllers (Fase 6)
- [ ] Integration tests - Background services (Fase 7)
- [ ] E2E tests - Critical flows (Fase 8)

### Post-Implementación
- [ ] Cobertura > 75% alcanzada
- [ ] CI/CD pipeline configurado
- [ ] Documentación de tests actualizada
- [ ] Equipo entrenado en mejores prácticas

---

## 📝 Notas Finales

Este documento es un **plan vivo** que debe actualizarse conforme el proyecto evoluciona. 

**Recomendaciones**:
1. **Priorizar Unit Tests primero**: Son rápidos, baratos, y detectan bugs temprano.
2. **Testcontainers > In-Memory**: PostgreSQL real es más confiable que in-memory.
3. **No mockear ApplicationDbContext**: Usar base de datos real en integration tests.
4. **Background services son críticos**: Tests de optimistic locking son esenciales.
5. **Webhooks requieren E2E**: Validar HMAC signature end-to-end.

**Próximos Pasos**:
1. Revisar este documento con el equipo
2. Ajustar prioridades según roadmap
3. Comenzar con FASE 1: Configuración Inicial
4. Ejecutar fases secuencialmente

---

**Creado**: 2025-11-25  
**Autor**: GitHub Copilot AI Assistant  
**Versión**: 1.0
