# GitHub Actions Workflows - VoiceByAuribus API

## Descripción General

Este directorio contiene los workflows de CI/CD que automatizan el deployment de VoiceByAuribus API a producción en AWS App Runner. El sistema implementa una estrategia de deployment segura que garantiza que las migraciones de base de datos se apliquen **antes** del deployment de código nuevo.

### Arquitectura del Sistema

```
┌─────────────────────────────────────────────────────────────┐
│                    git push origin main                     │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│          deploy-production.yml (ÚNICO WORKFLOW)             │
│                                                             │
│  ┌───────────────────────────────────────────────────────┐ │
│  │ Job 1: migrate                                        │ │
│  │ • Get connection string from AWS Secrets Manager      │ │
│  │ • Install dotnet-ef tools                             │ │
│  │ • Apply migrations: dotnet ef database update         │ │
│  │ ✅ Migrations aplicadas PRIMERO                       │ │
│  └───────────────────────────────────────────────────────┘ │
│                            ↓                                │
│  ┌───────────────────────────────────────────────────────┐ │
│  │ Job 2: build-and-push (reutiliza workflow)           │ │
│  │ • Build Docker image (linux/amd64)                    │ │
│  │ • Push to ECR:                                        │ │
│  │   - Tag :latest (trigger auto-deploy)                │ │
│  │   - Tag :sha-abc1234 (version específica)            │ │
│  └───────────────────────────────────────────────────────┘ │
│                            ↓                                │
│         ┌───────────────────────────────────┐               │
│         │ AWS App Runner                    │               │
│         │ AutoDeploymentsEnabled: true      │               │
│         │ Detecta :latest → auto-deploy     │               │
│         │ ✅ Deploy CON migrations aplicadas│               │
│         └───────────────────────────────────┘               │
│                            ↓                                │
│  ┌───────────────────────────────────────────────────────┐ │
│  │ Job 3: deploy                                         │ │
│  │ • Trigger manual deployment (backup)                  │ │
│  │ • Wait for status RUNNING (max 10 min)               │ │
│  │ • Verify health endpoint: /api/v1/health             │ │
│  │ ✅ Deployment verificado                             │ │
│  └───────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

### Características Clave

- ✅ **Zero-Downtime Deployments**: Migrations aplicadas antes del deploy
- ✅ **Fail-Fast Strategy**: Si las migrations fallan, no se despliega código roto
- ✅ **Auto-Deployment con App Runner**: Tag `:latest` trigger auto-deploy
- ✅ **Reutilización de Código**: Workflow build-and-push compartido
- ✅ **Auditoría Completa**: Logs detallados en GitHub Actions y CloudWatch

## Workflows Disponibles

### 1. `build-and-push.yml` - Workflow Reutilizable

**Tipo**: Reusable Workflow (workflow_call)

**Propósito**: 
- Build de imagen Docker para arquitectura AMD64
- Push a Amazon ECR con múltiples tags
- Outputs para workflows consumers

**Triggers**:
- ✅ `workflow_dispatch` - Ejecución manual desde GitHub UI
- ✅ `workflow_call` - Llamado por otros workflows (ej: deploy-production.yml)
- ❌ `on:push` - **DESHABILITADO intencionalmente**

**¿Por qué NO tiene `on:push`?**

Este workflow NO se ejecuta automáticamente en push para prevenir:

1. **Ejecución Duplicada**: `deploy-production.yml` ya llama este workflow
2. **Race Condition**: App Runner tiene `AutoDeploymentsEnabled: true`
   - Si este workflow pushea `:latest`, App Runner auto-despliega inmediatamente
   - Deployment ocurriría ANTES de aplicar migrations ❌

**Outputs**:
| Output | Descripción | Ejemplo |
|--------|-------------|---------|
| `image-uri` | URI completa de la imagen | `265584593347.dkr.ecr.us-east-1.amazonaws.com/voice-by-auribus-api:sha-abc1234` |
| `image-tag` | Tag corto del commit | `sha-abc1234` |

**Uso Manual** (para builds sin deployment):
```bash
# GitHub UI: Actions > Build and Push to ECR > Run workflow
# ⚠️ ADVERTENCIA: Esto triggeará auto-deploy de App Runner
```

---

### 2. `deploy-production.yml` - Workflow Principal de Deployment

**Tipo**: Production Deployment Pipeline

**Propósito**: 
- Deployment completo y seguro a producción
- Aplicar migrations de base de datos antes del deploy
- Verificación automática post-deployment

**Triggers**:
- ✅ `push` a rama `main` - **Trigger principal** (automático a Production)
- ✅ `push` a rama `test` - **Deployment a Staging** (automático)
- ✅ `workflow_dispatch` - Ejecución manual para re-deploys

**Estrategia de Deployment**: Database-First, Zero-Downtime, Multi-Environment

Este workflow implementa el patrón "Database First" que garantiza:
1. Migrations se aplican ANTES de desplegar código nuevo
2. Si migrations fallan, el deployment se cancela (fail-fast)
3. App Runner auto-despliega solo DESPUÉS de que migrations sean exitosas
4. **Environment dinámico basado en rama** (Production, Staging, Development)

---

### Ambientes por Rama

El workflow detecta automáticamente el ambiente basado en la rama **CON VALIDACIÓN DE SEGURIDAD**:

| Rama | Environment | Secret | Base de Datos | Permitido |
|------|-------------|--------|---------------|-----------|
| `main` | `Production` | `voice-by-auribus-api/production` | Producción | ✅ |
| `test` | `Staging` | `voice-by-auribus-api/staging` | Staging | ✅ |
| Otras | ❌ **RECHAZADO** | N/A | N/A | ❌ |

**⚠️ PROTECCIÓN DE SEGURIDAD**: El workflow **falla inmediatamente** si se intenta ejecutar desde una rama que no sea `main` o `test`, previniendo migraciones accidentales en producción.

**Implementación**:
```yaml
on:
  push:
    branches: 
      - main  # Solo estas ramas pueden ejecutar el workflow
      - test
  workflow_dispatch:

- name: Set Environment Variables
  run: |
    if [ "${{ github.ref }}" == "refs/heads/main" ]; then
      echo "ASPNETCORE_ENVIRONMENT=Production" >> $GITHUB_ENV
      echo "SECRET_NAME=voice-by-auribus-api/production" >> $GITHUB_ENV
    elif [ "${{ github.ref }}" == "refs/heads/test" ]; then
      echo "ASPNETCORE_ENVIRONMENT=Staging" >> $GITHUB_ENV
      echo "SECRET_NAME=voice-by-auribus-api/staging" >> $GITHUB_ENV
    else
      echo "❌ ERROR: Este workflow solo debe ejecutarse desde rama 'main' o 'test'"
      exit 1  # FALLA EL WORKFLOW
    fi

- name: Validate Environment Configuration
  run: |
    # Doble validación de seguridad
    if [ "${{ github.ref }}" == "refs/heads/main" ] && [ "$ASPNETCORE_ENVIRONMENT" != "Production" ]; then
      echo "❌ ERROR DE SEGURIDAD: Rama main debe usar Production environment"
      exit 1
    fi
```

**Beneficios**:
- ✅ Imposible ejecutar migrations en producción desde ramas de feature
- ✅ Doble capa de validación (trigger + validación explícita)
- ✅ Logs claros indicando por qué el workflow falló
- ✅ Protección contra errores humanos (push accidental a main)

---

#### Job 1: migrate

**Responsabilidad**: Aplicar migraciones de Entity Framework Core al ambiente correspondiente

**Steps**:
1. Checkout código
2. Setup .NET 10
3. Configure AWS Credentials (IAM user: `github-actions-ecr-voicebyauribusapi`)
4. **Set Environment Variables** (basado en rama):
   ```bash
   # Si rama = main:
   ASPNETCORE_ENVIRONMENT=Production
   SECRET_NAME=voice-by-auribus-api/production
   
   # Si rama = test:
   ASPNETCORE_ENVIRONMENT=Staging
   SECRET_NAME=voice-by-auribus-api/staging
   
   # Cualquier otra rama:
   ASPNETCORE_ENVIRONMENT=Development
   SECRET_NAME=voice-by-auribus-api/development
   ```
5. **Get Connection String** (desde secret dinámico):
   ```bash
   aws secretsmanager get-secret-value \
     --secret-id "$SECRET_NAME" \
     --query 'SecretString' | jq -r '.ConnectionStrings__DefaultConnection'
   ```
6. Install EF Core Tools: `dotnet tool install --global dotnet-ef`
7. **Restore NuGet Packages**: `dotnet restore VoiceByAuribus.API/VoiceByAuribus-API.csproj`
8. **Apply Migrations** (con `ASPNETCORE_ENVIRONMENT` dinámico):
   ```bash
   dotnet ef database update \
     --project VoiceByAuribus.API/VoiceByAuribus-API.csproj \
     --connection "$DB_CONNECTION" \
     --verbose
   ```
9. Verify Migration Status: `dotnet ef migrations list`

**Fail-Fast Behavior**:
- ❌ Si connection string no se encuentra → Exit 1 → Jobs 2 y 3 cancelados
- ❌ Si migration falla → Exit 1 → Jobs 2 y 3 cancelados
- ✅ Si migration exitosa → Continuar a Job 2

**Permisos IAM Requeridos**:
- `secretsmanager:GetSecretValue` en `arn:aws:secretsmanager:us-east-1:265584593347:secret:voice-by-auribus-api/production-*`

---

#### Job 2: build-and-push

**Responsabilidad**: Build y push de imagen Docker a ECR

**Configuración**:
```yaml
needs: migrate  # Solo ejecuta si migrate fue exitoso
uses: ./.github/workflows/build-and-push.yml  # Reutiliza workflow
secrets: inherit  # Hereda AWS_ACCESS_KEY_ID y AWS_SECRET_ACCESS_KEY
```

**Proceso**:
1. Login a ECR: `aws ecr get-login-password`
2. Build imagen Docker (AMD64):
   ```bash
   docker buildx build \
     --platform linux/amd64 \
     -f VoiceByAuribus.API/Dockerfile.apprunner \
     -t $ECR_REGISTRY/$ECR_REPOSITORY:latest \
     -t $ECR_REGISTRY/$ECR_REPOSITORY:sha-abc1234 \
     --push
   ```
3. Push ambos tags a ECR

**App Runner Auto-Deploy**:
- App Runner está configurado con `AutoDeploymentsEnabled: true`
- Cuando detecta nuevo tag `:latest` → inicia deployment automático
- En este punto, migrations ya fueron aplicadas ✅

**Permisos IAM Requeridos**:
- `ecr:GetAuthorizationToken`
- `ecr:BatchCheckLayerAvailability`, `ecr:PutImage`, etc.

---

#### Job 3: deploy

**Responsabilidad**: Verificar y monitorear el deployment

**Configuración**:
```yaml
needs: build-and-push  # Solo ejecuta si build fue exitoso
```

**Steps**:
1. Configure AWS Credentials
2. **Get Service ARN**:
   ```bash
   aws apprunner list-services \
     --query "ServiceSummaryList[?ServiceName=='voice-by-auribus-api'].ServiceArn"
   ```
3. **Trigger Deployment** (backup si auto-deploy falla):
   ```bash
   aws apprunner start-deployment --service-arn $SERVICE_ARN
   ```
4. **Wait for Deployment** (max 10 minutos):
   - Poll status cada 10 segundos
   - Esperar hasta `Status == "RUNNING"`
   - Timeout después de 60 intentos
5. **Get Service URL**:
   ```bash
   aws apprunner describe-service --query 'Service.ServiceUrl'
   ```
6. **Verify Health Endpoint**:
   ```bash
   curl -s -o /dev/null -w "%{http_code}" "https://$SERVICE_URL/api/v1/health"
   ```
   - Esperado: HTTP 200
   - Advertencia si != 200
7. **Deployment Summary**: Imprime detalles del deployment exitoso

**Permisos IAM Requeridos**:
- `apprunner:ListServices`
- `apprunner:DescribeService`
- `apprunner:StartDeployment`

---

#### Ejemplo de Uso Completo

**Desarrollo Normal**:
```bash
# 1. Crear migración localmente
cd VoiceByAuribus.API
dotnet ef migrations add AddUserStatusColumn

# 2. Probar localmente
docker-compose up -d postgres
dotnet ef database update
dotnet run
# Verificar: curl http://localhost:5037/api/v1/health

# 3. Commit y push
git add .
git commit -m "feat: Add user status column with migration"
git push origin main

# → GitHub Actions ejecuta automáticamente:
#   ✅ Job 1: Apply migrations a producción
#   ✅ Job 2: Build imagen Docker + Push a ECR
#   ✅ App Runner auto-deploys (detecta :latest)
#   ✅ Job 3: Verifica deployment exitoso
```

**Deployment Manual** (re-deploy sin cambios):
```bash
# GitHub UI: Actions > Deploy to Production > Run workflow
# Usa: última imagen :latest ya en ECR
```

---

## Configuración de AWS

### IAM User: `github-actions-ecr-voicebyauribusapi`

El usuario IAM tiene la política **GitHubActions-ECR-VoiceByAuribusApi** (versión **v4**) que incluye los siguientes permisos:

#### Statement 1: GetAuthorizationToken (ECR)

```json
{
  "Sid": "GetAuthorizationToken",
  "Effect": "Allow",
  "Action": "ecr:GetAuthorizationToken",
  "Resource": "*"
}
```

**Propósito**: Obtener token para login a ECR

**Usado por**: Jobs `build-and-push` y cualquier operación que requiera autenticación ECR

---

#### Statement 2: PushImagesToSpecificRepository (ECR)

```json
{
  "Sid": "PushImagesToSpecificRepository",
  "Effect": "Allow",
  "Action": [
    "ecr:BatchCheckLayerAvailability",
    "ecr:GetDownloadUrlForLayer",
    "ecr:PutImage",
    "ecr:InitiateLayerUpload",
    "ecr:UploadLayerPart",
    "ecr:CompleteLayerUpload"
  ],
  "Resource": "arn:aws:ecr:us-east-1:265584593347:repository/voice-by-auribus-api"
}
```

**Propósito**: Permisos completos para push de imágenes Docker al repositorio ECR

**Usado por**: Job `build-and-push` cuando pushea tags `:latest` y `:sha-abc1234`

---

#### Statement 3: LambdaUpdateFunctionCode

```json
{
  "Sid": "LambdaUpdateFunctionCode",
  "Effect": "Allow",
  "Action": [
    "lambda:UpdateFunctionCode",
    "lambda:GetFunction",
    "lambda:PublishVersion"
  ],
  "Resource": "arn:aws:lambda:us-east-1:265584593347:function:VoiceByAuribus-*"
}
```

**Propósito**: Actualización de código de Lambda functions (AudioUploadNotifier)

**Usado por**: Workflow de deployment de Lambda (no documentado aquí)

---

#### Statement 4: SecretsManagerGetConnectionString (Nuevo en v3)

```json
{
  "Sid": "SecretsManagerGetConnectionString",
  "Effect": "Allow",
  "Action": "secretsmanager:GetSecretValue",
  "Resource": "arn:aws:secretsmanager:us-east-1:265584593347:secret:voice-by-auribus-api/*"
}
```

**Propósito**: Obtener connection string de base de datos desde AWS Secrets Manager

**Usado por**: Job `migrate` para obtener `ConnectionStrings__DefaultConnection`

**Secrets soportados** (basado en rama):
- `voice-by-auribus-api/production` (rama `main`)
- `voice-by-auribus-api/staging` (rama `test`)
- `voice-by-auribus-api/development` (otras ramas)

**Path del valor**: `ConnectionStrings__DefaultConnection` (⚠️ **doble underscore**)

---

#### Statement 5: AppRunnerListServices (Nuevo en v4)

```json
{
  "Sid": "AppRunnerListServices",
  "Effect": "Allow",
  "Action": "apprunner:ListServices",
  "Resource": "*"
}
```

**Propósito**: Listar servicios de App Runner en la cuenta

**Usado por**: Job `deploy` para obtener el ARN del servicio

**Resource**: Debe ser `*` (requerimiento de AWS para ListServices)

---

#### Statement 6: AppRunnerDeployment (Actualizado en v4)

```json
{
  "Sid": "AppRunnerDeployment",
  "Effect": "Allow",
  "Action": [
    "apprunner:DescribeService",
    "apprunner:StartDeployment"
  ],
  "Resource": "arn:aws:apprunner:us-east-1:265584593347:service/voice-by-auribus-api/*"
}
```

**Propósito**: Gestionar deployments de App Runner

**Usado por**: Job `deploy` para:
- Obtener estado del servicio (`DescribeService`)
- Trigger deployment manual (`StartDeployment`)

**Service ARN**: `arn:aws:apprunner:us-east-1:265584593347:service/voice-by-auribus-api/68560864d20c469ca8cf621270afcd2e`

---

#### Statement 7: STSGetCallerIdentity (Nuevo en v3)

```json
{
  "Sid": "STSGetCallerIdentity",
  "Effect": "Allow",
  "Action": "sts:GetCallerIdentity",
  "Resource": "*"
}
```

**Propósito**: Validación de identidad AWS (debugging y auditoría)

**Usado por**: Cualquier step que necesite verificar credenciales AWS

---

### AWS Secrets Manager: `voice-by-auribus-api/production`

**Tipo**: Secret con múltiples key-value pairs

**Estructura esperada**:

```json
{
  "ConnectionStrings__DefaultConnection": "Host=xxx.rds.amazonaws.com;Port=5432;Database=voice_by_auribus_api;Username=xxx;Password=xxx"
}
```

⚠️ **Importante**: El nombre del campo usa **doble underscore** (`__`) para representar la estructura jerárquica en ASP.NET Core:

```yaml
ConnectionStrings:
  DefaultConnection: "connection-string-value"
```

**Acceso desde workflow**:

```bash
DB_CONNECTION=$(aws secretsmanager get-secret-value \
  --secret-id "voice-by-auribus-api/production" \
  --query 'SecretString' \
  --output text | jq -r '.ConnectionStrings__DefaultConnection')
```

**Verificación local** (requiere AWS CLI configurado):

```bash
aws secretsmanager get-secret-value \
  --secret-id "voice-by-auribus-api/production" \
  --query 'SecretString' | jq '.ConnectionStrings__DefaultConnection'
```

---

### AWS App Runner: Auto-Deployment

**Service Name**: `voice-by-auribus-api`

**Auto-Deployment Configuration**:

```bash
aws apprunner describe-service \
  --service-arn arn:aws:apprunner:us-east-1:265584593347:service/voice-by-auribus-api/68560864d20c469ca8cf621270afcd2e \
  --query 'Service.SourceConfiguration.ImageRepository.ImageConfiguration.AutoDeploymentsEnabled'
# Output: true
```

**Comportamiento**:

1. App Runner monitorea el repositorio ECR: `265584593347.dkr.ecr.us-east-1.amazonaws.com/voice-by-auribus-api`
2. Cuando detecta cambio en tag `:latest` → trigger deployment automático
3. App Runner pull nueva imagen y reemplaza running service
4. Health checks automáticos antes de servir tráfico

**Implicación para Workflows**:

- ❌ **NO** pushear `:latest` antes de aplicar migrations
- ✅ **SÍ** pushear `:latest` solo después de migrations exitosas
- Por esto `build-and-push.yml` NO tiene `on:push` (previene auto-deploy prematuro)

---

## Deployment de Migraciones de Base de Datos

### Estrategia: Database-First, Zero-Downtime

Este proyecto implementa el patrón **Database-First** para migraciones, que es el estándar de la industria para aplicaciones en producción.

#### Comparación de Estrategias

| Estrategia | Cuándo se aplican migrations | Ventajas | Desventajas | Recomendado |
|------------|------------------------------|----------|-------------|-------------|
| **Database-First** | **ANTES** del deployment de código | ✅ Zero downtime<br>✅ Fail-fast<br>✅ Código nuevo siempre encuentra schema correcto | ⚠️ Migrations deben ser backward-compatible | ✅ **IMPLEMENTADO** |
| Code-First | DESPUÉS del deployment de código | Código nuevo deployado primero | ❌ Downtime durante migrations<br>❌ Requests fallan hasta que migrations completen | ❌ No usar |
| Parallel | Durante el deployment | Rápido | ❌ Race conditions<br>❌ Código nuevo puede fallar si migrations incompletas | ❌ No usar |
| Manual | Manualmente antes de deployment | Control total | ❌ Propenso a errores humanos<br>❌ No automatizado | ❌ No usar |

#### Reglas de Oro para Migrations

**1. Backward Compatibility** (Migrations deben ser compatibles con código actual):

✅ **BUENAS PRÁCTICAS**:

```csharp
// ✅ Agregar columna nullable (compatible con código existente)
migrationBuilder.AddColumn<string>(
    name: "UserStatus",
    table: "users",
    nullable: true);

// ✅ Agregar índice (no afecta código)
migrationBuilder.CreateIndex(
    name: "IX_users_email",
    table: "users",
    column: "email");

// ✅ Crear nueva tabla (código antiguo la ignora)
migrationBuilder.CreateTable(
    name: "notifications",
    columns: table => new { /* ... */ });
```

❌ **MALAS PRÁCTICAS**:

```csharp
// ❌ Eliminar columna que código actual usa
migrationBuilder.DropColumn(
    name: "OldColumn",
    table: "users");

// ❌ Cambiar tipo de dato (código actual esperaba int, ahora es string)
migrationBuilder.AlterColumn<string>(
    name: "UserId",
    table: "sessions",
    oldClrType: typeof(int));

// ❌ Agregar columna NOT NULL sin valor default
migrationBuilder.AddColumn<string>(
    name: "RequiredField",
    table: "users",
    nullable: false);  // ❌ Código existente no sabe de este campo
```

**2. Two-Phase Changes** (Cambios en 2 deployments para safety):

**Ejemplo**: Renombrar columna `OldName` → `NewName`

**Deployment 1** (Add new column):

```csharp
// Migration 1: Agregar nueva columna
migrationBuilder.AddColumn<string>(
    name: "NewName",
    table: "users",
    nullable: true);

// Código: Escribir en ambas columnas
user.OldName = value;
user.NewName = value;  // Nueva columna
```

**Deployment 2** (Remove old column, después de verificar que todo funciona):

```csharp
// Migration 2: Eliminar columna antigua
migrationBuilder.DropColumn(
    name: "OldName",
    table: "users");

// Código: Solo escribir en nueva columna
user.NewName = value;
```

**3. Testing Local Antes de Push**:

```bash
# 1. Crear migración
cd VoiceByAuribus.API
dotnet ef migrations add AddUserStatusColumn

# 2. Verificar SQL generado
dotnet ef migrations script --from <previous-migration> --to AddUserStatusColumn

# 3. Aplicar localmente
docker-compose up -d postgres
dotnet ef database update

# 4. Probar aplicación
dotnet run
curl http://localhost:5037/api/v1/health

# 5. Si todo OK, commit y push
git add .
git commit -m "feat: Add user status with backward-compatible migration"
git push origin main
```

#### Rollback de Migrations

**Rollback Local**:

```bash
# Revertir última migración
dotnet ef database update <previous-migration-name>

# Ver lista de migrations aplicadas
dotnet ef migrations list
```

**Rollback en Producción** (⚠️ **USAR CON PRECAUCIÓN**):

```bash
# 1. Obtener connection string
DB_CONNECTION=$(aws secretsmanager get-secret-value \
  --secret-id "voice-by-auribus-api/production" \
  --query 'SecretString' --output text | jq -r '.ConnectionStrings__DefaultConnection')

# 2. Revertir a migración específica
dotnet ef database update <target-migration-name> \
  --project VoiceByAuribus.API/VoiceByAuribus-API.csproj \
  --connection "$DB_CONNECTION"

# 3. Re-deploy código compatible con schema antiguo
git revert <commit-hash>
git push origin main
```

⚠️ **IMPORTANTE**: Rollback de migrations puede causar pérdida de datos si:

- La migration eliminó columnas (datos perdidos)
- La migration modificó tipos de datos (conversión irreversible)

**Alternativa segura**: Deploy nueva migration que restaura schema anterior

---

### Verificación de Migrations en Producción

**Ver migrations aplicadas**:

```bash
DB_CONNECTION=$(aws secretsmanager get-secret-value \
  --secret-id "voice-by-auribus-api/production" \
  --query 'SecretString' --output text | jq -r '.ConnectionStrings__DefaultConnection')

dotnet ef migrations list \
  --project VoiceByAuribus.API/VoiceByAuribus-API.csproj \
  --connection "$DB_CONNECTION"
```

**Ver SQL de migración pendiente**:

```bash
dotnet ef migrations script \
  --project VoiceByAuribus.API/VoiceByAuribus-API.csproj \
  --idempotent  # Genera script safe para re-ejecución
```

---

## Arquitectura de Reutilización

### ¿Por qué separar los workflows?

**Problema resuelto**: Evitar duplicación de código para build/push Y evitar race conditions con auto-deployment.

**Problema original** (con on:push en ambos):

```text
git push origin main
    ↓
├─ build-and-push.yml se ejecuta (push :latest)
│  └─ App Runner auto-deploys (SIN migrations!) ❌
│
└─ deploy-production.yml se ejecuta
   ├─ apply migrations
   ├─ llama a build-and-push.yml (duplicado!) ❌
   └─ deploy to App Runner
```

**Solución implementada**:

```text
git push origin main
    ↓
deploy-production.yml (único workflow que se ejecuta)
    ↓
├─ Job 1: migrate (migrations PRIMERO)
├─ Job 2: build-and-push.yml (reutiliza)
│   └─ Push :latest a ECR
│       └─ App Runner auto-deploys (CON migrations) ✅
└─ Job 3: deploy (verifica deployment)
```

**Ventajas**:

- ✅ DRY (Don't Repeat Yourself)
- ✅ Un solo lugar para cambios de build
- ✅ `build-and-push.yml` funciona independiente (manual) o reutilizado
- ✅ Outputs disponibles para consumers
- ✅ **NO hay race conditions con auto-deployment** (migrations siempre primero)
- ✅ **NO hay ejecuciones duplicadas** (solo deploy-production.yml en push a main)

---

## Flujos de Trabajo Comunes

### Desarrollo Regular (con migración)

```bash
# 1. Crear migración
cd VoiceByAuribus.API
dotnet ef migrations add AddNewFeature

# 2. Probar localmente
docker-compose up -d postgres
dotnet ef database update
dotnet run
curl http://localhost:5037/api/v1/health

# 3. Commit y push
git add .
git commit -m "feat: Add new feature with migration"
git push origin main

# → GitHub Actions automáticamente:
#   ✅ Aplica migration a producción
#   ✅ Builds imagen Docker
#   ✅ Pushes a ECR (:latest y :sha-abc1234)
#   ✅ App Runner auto-deploys
#   ✅ Verifica health endpoint
```

### Build Solo (sin deployment automático)

```bash
# Ejecutar manualmente build-and-push.yml
# GitHub UI: Actions > Build and Push to ECR > Run workflow

# ⚠️ ADVERTENCIA: Esto pusheará :latest y triggeará auto-deploy de App Runner
# Usar solo si sabes que migrations ya están aplicadas
```

### Deployment Sin Cambios de Código

```bash
# Re-deploy última imagen ya en ECR
# GitHub UI: Actions > Deploy to Production > Run workflow

# → GitHub Actions:
#   ✅ Aplica migrations pendientes (si hay)
#   ✅ Re-usa última imagen en ECR
#   ✅ Deploys a App Runner
```

---

## Rollback

### Opción 1: Git Revert (Recomendado)

```bash
# 1. Identificar commit problemático
git log --oneline

# 2. Revertir commit
git revert <commit-hash>
git push origin main

# → GitHub Actions automáticamente:
#   ✅ Revierte migración (si aplica)
#   ✅ Builds código anterior
#   ✅ Deploys versión anterior
```

⚠️ **Importante**: Solo funciona si la migration es reversible (Down method implementado)

### Opción 2: Rollback Manual de Migración

```bash
# 1. Obtener connection string desde Secrets Manager
DB_CONNECTION=$(aws secretsmanager get-secret-value \
  --secret-id "voice-by-auribus-api/production" \
  --query 'SecretString' --output text | jq -r '.ConnectionStrings__DefaultConnection')

# 2. Ver migrations aplicadas
dotnet ef migrations list \
  --project VoiceByAuribus.API/VoiceByAuribus-API.csproj \
  --connection "$DB_CONNECTION"

# 3. Rollback a migración específica
dotnet ef database update <target-migration-name> \
  --project VoiceByAuribus.API/VoiceByAuribus-API.csproj \
  --connection "$DB_CONNECTION" \
  --verbose

# 4. Deploy código compatible con schema revertido
git revert <commit-hash>  # o git checkout de versión anterior
git push origin main
```

---

## Monitoreo de Deployments

### GitHub Actions

**URL**: <https://github.com/julio-soft/VoiceByAuribus-API/actions>

**Logs disponibles**:

- ✅ Output de cada step
- ✅ Errores de migración con stack traces
- ✅ Build logs de Docker
- ✅ Status de App Runner deployment
- ✅ Health check results

### AWS CloudWatch

**URL**: <https://console.aws.amazon.com/cloudwatch/home?region=us-east-1#logsV2:log-groups/log-group/$252Faws$252Fapprunner$252Fvoice-by-auribus-api>

**Logs disponibles**:

- ✅ Application logs (ASP.NET Core)
- ✅ Request/response logs
- ✅ Error logs

### AWS App Runner Console

**URL**: <https://console.aws.amazon.com/apprunner/home?region=us-east-1#/services/voice-by-auribus-api>

**Información disponible**:

- ✅ Deployment history
- ✅ Service health status
- ✅ Running configuration
- ✅ Auto-deployment status

---

## Troubleshooting

### Workflow Falla en Migración

**Síntoma**: Job `migrate` falla, deployment se cancela.

**Pasos para resolver**:

1. Ver logs en GitHub Actions
   - Click en workflow fallido
   - Expandir step "Apply migrations"
   - Identificar error SQL

2. **Error: "Environment: Development" en logs de migration**:
   
   **Causa**: EF Core ejecuta la aplicación para obtener DbContext y necesita `ASPNETCORE_ENVIRONMENT=Production`
   
   **Solución**: El workflow ya incluye esta variable de entorno:
   ```yaml
   - name: Apply Migrations
     env:
       ASPNETCORE_ENVIRONMENT: Production
   ```
   
   Si ves logs indicando "Environment: Development" o intentando cargar secrets de development, verifica que este `env` esté presente.

3. Diagnosticar error localmente:

   ```bash
   cd VoiceByAuribus.API
   dotnet ef database update --verbose
   ```

3. Corregir migración:
   - Si error en Up: Corregir código migration
   - Si error SQL: Ajustar schema o datos manualmente

4. Re-ejecutar:

   ```bash
   git add .
   git commit -m "fix: Correct migration error"
   git push origin main
   ```

### Build Falla

**Síntoma**: Job `build-and-push` falla.

**Pasos para resolver**:

1. Verificar Dockerfile.apprunner:

   ```bash
   # Probar build localmente
   docker build \
     --platform linux/amd64 \
     -f VoiceByAuribus.API/Dockerfile.apprunner \
     -t test .
   ```

2. Verificar context de build:
   - Dockerfile debe estar en `VoiceByAuribus.API/`
   - Context debe ser raíz del proyecto
   - Verificar `.dockerignore`

3. Verificar permisos ECR:
   - IAM user tiene `ecr:PutImage`
   - Repository `voice-by-auribus-api` existe

### Deployment a App Runner Falla

**Síntoma**: Job `deploy` falla en "Wait for Deployment".

**Pasos para resolver**:

1. Verificar logs en CloudWatch:
   - Buscar errores de startup
   - Verificar connection string
   - Verificar variables de entorno

2. Verificar health endpoint:

   ```bash
   SERVICE_URL=$(aws apprunner describe-service \
     --service-arn arn:aws:apprunner:us-east-1:265584593347:service/voice-by-auribus-api/68560864d20c469ca8cf621270afcd2e \
     --query 'Service.ServiceUrl' --output text)

   curl -v "https://$SERVICE_URL/api/v1/health"
   ```

3. Verificar configuración de App Runner:
   - Port: 5037 (debe coincidir con `ASPNETCORE_URLS`)
   - Health check path: `/api/v1/health`
   - Timeout: Suficiente para startup

### Connection String No Encontrado

**Síntoma**: Job `migrate` falla con "Connection string not found".

**Pasos para resolver**:

1. Verificar secret en AWS Secrets Manager:

   ```bash
   aws secretsmanager get-secret-value \
     --secret-id "voice-by-auribus-api/production" \
     --query 'SecretString'
   ```

2. Verificar formato (debe tener `ConnectionStrings__DefaultConnection` con doble underscore):

   ```json
   {
     "ConnectionStrings__DefaultConnection": "Host=...;Database=...;"
   }
   ```

3. Verificar permisos IAM:
   - User `github-actions-ecr-voicebyauribusapi` tiene `secretsmanager:GetSecretValue`
   - Resource ARN correcto en policy

### Assets File Not Found (project.assets.json)

**Síntoma**: Job `migrate` falla con "error NETSDK1004: Assets file 'project.assets.json' not found".

**Causa**: NuGet packages no fueron restaurados antes de ejecutar `dotnet ef`.

**Solución**: El workflow incluye `dotnet restore` antes de aplicar migrations. Si el error persiste:

1. Verificar que el step "Restore NuGet Packages" se ejecutó correctamente
2. Verificar conectividad a NuGet.org desde GitHub Actions
3. Verificar que `.csproj` no tiene errores de configuración

**Este error ya está resuelto** en la versión actual del workflow con el step:

```yaml
- name: Restore NuGet Packages
  run: |
    echo "📦 Restaurando paquetes NuGet..."
    dotnet restore VoiceByAuribus.API/VoiceByAuribus-API.csproj
```

---

## Referencias

- [GitHub Actions - Reusing Workflows](https://docs.github.com/en/actions/using-workflows/reusing-workflows)
- [EF Core Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [AWS App Runner Documentation](https://docs.aws.amazon.com/apprunner/)
- [AWS Secrets Manager](https://docs.aws.amazon.com/secretsmanager/)
- [Database Migration Best Practices](https://martinfowler.com/articles/evodb.html)

---

**Última actualización**: 2024-11-22

**Versión de políticas IAM**: v4

**Maintainers**: Julio César (@julio-soft)
