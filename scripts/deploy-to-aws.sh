#!/bin/bash

# ==============================================================================
# Script de Deployment Automatizado a AWS App Runner
# Proyecto: VoiceByAuribus API
# ==============================================================================
# Este script automatiza el proceso completo de deployment:
# 1. Build de imagen Docker
# 2. Push a Amazon ECR
# 3. Trigger deployment en App Runner
# ==============================================================================

set -e  # Exit on error
set -u  # Exit on undefined variable

# ==============================================================================
# COLORES PARA OUTPUT
# ==============================================================================
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# ==============================================================================
# FUNCIONES DE UTILIDAD
# ==============================================================================
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# ==============================================================================
# CONFIGURACIÓN
# ==============================================================================
# Detecta la región desde AWS CLI config o usa default
AWS_REGION="${AWS_REGION:-us-east-1}"
AWS_ACCOUNT_ID="${AWS_ACCOUNT_ID:-$(aws sts get-caller-identity --query Account --output text)}"
PROJECT_NAME="voice-by-auribus-api"
ECR_REPOSITORY="$PROJECT_NAME"
DOCKERFILE="VoiceByAuribus.API/Dockerfile.apprunner"

# Versión (puede ser sobreescrita con argumento)
# Si no hay git repo, usa "latest" como fallback
VERSION="${1:-$(git rev-parse --short HEAD 2>/dev/null || echo 'latest')}"

log_info "==================================================================="
log_info "  Deployment Script - VoiceByAuribus API to AWS App Runner"
log_info "==================================================================="
log_info "AWS Region: $AWS_REGION"
log_info "AWS Account: $AWS_ACCOUNT_ID"
log_info "Version: $VERSION"
log_info "==================================================================="
echo ""

# ==============================================================================
# VALIDACIONES PRE-DEPLOYMENT
# ==============================================================================
log_info "Validando prerequisitos..."

# Verificar AWS CLI
if ! command -v aws &> /dev/null; then
    log_error "AWS CLI no está instalado"
    exit 1
fi

# Verificar Docker
if ! command -v docker &> /dev/null; then
    log_error "Docker no está instalado"
    exit 1
fi

# Verificar credenciales AWS
if ! aws sts get-caller-identity &> /dev/null; then
    log_error "No se pudo autenticar con AWS. Ejecuta 'aws configure'"
    exit 1
fi

# Verificar que el Dockerfile existe
if [ ! -f "$DOCKERFILE" ]; then
    log_error "Dockerfile no encontrado en: $DOCKERFILE"
    exit 1
fi

log_success "Prerequisitos validados ✓"
echo ""

# ==============================================================================
# STEP 1: AUTENTICAR DOCKER CON ECR
# ==============================================================================
log_info "Step 1/5: Autenticando Docker con Amazon ECR..."

aws ecr get-login-password --region "$AWS_REGION" | \
    docker login --username AWS --password-stdin \
    "$AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com"

if [ $? -ne 0 ]; then
    log_error "Falló la autenticación con ECR"
    exit 1
fi

log_success "Docker autenticado con ECR ✓"
echo ""

# ==============================================================================
# STEP 2: BUILD DE LA IMAGEN DOCKER (AMD64 para App Runner)
# ==============================================================================
log_info "Step 2/5: Building imagen Docker para AMD64..."
log_info "Dockerfile: $DOCKERFILE"
log_info "Context: $(pwd)"
log_warning "Building para arquitectura AMD64 (requerido por App Runner)"

IMAGE_URI="$AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com/$ECR_REPOSITORY"

# Verificar si Docker Buildx está disponible
if ! docker buildx version &> /dev/null; then
    log_warning "Docker Buildx no encontrado, usando build estándar (puede no funcionar en Apple Silicon)"
    docker build \
        -f "$DOCKERFILE" \
        -t "$PROJECT_NAME:$VERSION" \
        -t "$PROJECT_NAME:latest" \
        -t "$IMAGE_URI:$VERSION" \
        -t "$IMAGE_URI:latest" \
        .
else
    # Usar buildx para garantizar arquitectura AMD64
    log_info "Usando Docker Buildx para garantizar arquitectura AMD64..."
    docker buildx build \
        --platform linux/amd64 \
        --load \
        -f "$DOCKERFILE" \
        -t "$PROJECT_NAME:$VERSION" \
        -t "$PROJECT_NAME:latest" \
        -t "$IMAGE_URI:$VERSION" \
        -t "$IMAGE_URI:latest" \
        .
fi

if [ $? -ne 0 ]; then
    log_error "Falló el build de Docker"
    log_info "Alternativa: Usa GitHub Actions para builds automáticos AMD64"
    log_info "Ver: .github/workflows/build-and-push.yml"
    exit 1
fi

log_success "Imagen construida exitosamente para AMD64 ✓"
echo ""

# ==============================================================================
# STEP 3: PUSH A ECR
# ==============================================================================
log_info "Step 3/5: Pushing imagen a Amazon ECR..."

# Push versión específica
log_info "Pushing tag: $VERSION"
docker push "$IMAGE_URI:$VERSION"

# Push latest
log_info "Pushing tag: latest"
docker push "$IMAGE_URI:latest"

if [ $? -ne 0 ]; then
    log_error "Falló el push a ECR"
    exit 1
fi

log_success "Imagen pushed a ECR exitosamente ✓"
log_info "Image URI: $IMAGE_URI:$VERSION"
echo ""

# ==============================================================================
# STEP 4: OBTENER SERVICE ARN DE APP RUNNER
# ==============================================================================
log_info "Step 4/5: Obteniendo información del servicio App Runner..."

# Listar servicios para encontrar el nuestro
SERVICE_ARN=$(aws apprunner list-services \
    --region "$AWS_REGION" \
    --query "ServiceSummaryList[?ServiceName=='$PROJECT_NAME'].ServiceArn" \
    --output text)

if [ -z "$SERVICE_ARN" ]; then
    log_warning "No se encontró el servicio '$PROJECT_NAME' en App Runner"
    log_info "Si es el primer deployment, crea el servicio usando la guía en AWS_DEPLOYMENT_GUIDE.md"
    log_info "Después vuelve a ejecutar este script para deployments posteriores"
    exit 0
fi

log_success "Servicio encontrado ✓"
log_info "Service ARN: $SERVICE_ARN"
echo ""

# ==============================================================================
# STEP 5: CONFIRMACIÓN Y TRIGGER DEPLOYMENT
# ==============================================================================
log_info "Step 5/5: Preparando deployment..."
echo ""

# Mostrar información del servicio
SERVICE_STATUS=$(aws apprunner describe-service \
    --service-arn "$SERVICE_ARN" \
    --query 'Service.Status' \
    --output text \
    --region "$AWS_REGION")

SERVICE_URL=$(aws apprunner describe-service \
    --service-arn "$SERVICE_ARN" \
    --query 'Service.ServiceUrl' \
    --output text \
    --region "$AWS_REGION")

log_warning "==================================================================="
log_warning "  CONFIRMACIÓN DE DEPLOYMENT"
log_warning "==================================================================="
log_info "Servicio: $PROJECT_NAME"
log_info "Estado actual: $SERVICE_STATUS"
log_info "URL: https://$SERVICE_URL"
log_info "Nueva versión: $VERSION"
log_info "Imagen: $IMAGE_URI:$VERSION"
log_warning "==================================================================="
echo ""

# Confirmación manual (solo si no está en modo CI/CD)
if [ -z "${CI:-}" ]; then
    read -p "¿Deseas continuar con el deployment? (yes/no): " -r CONFIRM
    echo ""
    if [[ ! $CONFIRM =~ ^[Yy](es)?$ ]]; then
        log_warning "Deployment cancelado por el usuario"
        exit 0
    fi
fi

log_info "Iniciando deployment..."
echo ""

# Start deployment
OPERATION_ID=$(aws apprunner start-deployment \
    --service-arn "$SERVICE_ARN" \
    --region "$AWS_REGION" \
    --query 'OperationId' \
    --output text)

if [ $? -ne 0 ]; then
    log_error "Falló el inicio del deployment"
    exit 1
fi

log_success "Deployment iniciado ✓"
log_info "Operation ID: $OPERATION_ID"
echo ""

# ==============================================================================
# STEP 6: ESPERAR A QUE EL DEPLOYMENT TERMINE
# ==============================================================================
log_info "Esperando a que el deployment complete..."
log_warning "Esto puede tomar 3-5 minutos..."
echo ""

# Loop para verificar el estado
MAX_ATTEMPTS=60
ATTEMPT=0
SLEEP_TIME=10

while [ $ATTEMPT -lt $MAX_ATTEMPTS ]; do
    STATUS=$(aws apprunner describe-service \
        --service-arn "$SERVICE_ARN" \
        --region "$AWS_REGION" \
        --query 'Service.Status' \
        --output text)

    if [ "$STATUS" == "RUNNING" ]; then
        log_success "Deployment completado exitosamente! ✓"
        break
    elif [ "$STATUS" == "OPERATION_IN_PROGRESS" ]; then
        echo -n "."
        sleep $SLEEP_TIME
        ATTEMPT=$((ATTEMPT + 1))
    else
        log_error "Deployment falló con estado: $STATUS"
        exit 1
    fi
done

if [ $ATTEMPT -eq $MAX_ATTEMPTS ]; then
    log_error "Timeout esperando el deployment"
    exit 1
fi

echo ""
echo ""

# ==============================================================================
# INFORMACIÓN FINAL
# ==============================================================================
log_success "==================================================================="
log_success "  DEPLOYMENT EXITOSO"
log_success "==================================================================="

# Obtener información del servicio
SERVICE_URL=$(aws apprunner describe-service \
    --service-arn "$SERVICE_ARN" \
    --region "$AWS_REGION" \
    --query 'Service.ServiceUrl' \
    --output text)

# Verificar si tiene dominio personalizado
CUSTOM_DOMAIN=$(aws apprunner list-associated-custom-domains \
    --service-arn "$SERVICE_ARN" \
    --region "$AWS_REGION" \
    --query 'CustomDomains[0].DomainName' \
    --output text 2>/dev/null)

echo ""
log_info "Versión deployada: $VERSION"
log_info "App Runner URL: https://$SERVICE_URL"

if [ "$CUSTOM_DOMAIN" != "None" ] && [ -n "$CUSTOM_DOMAIN" ]; then
    log_info "Dominio personalizado: https://$CUSTOM_DOMAIN"
fi

echo ""
log_info "Endpoints disponibles:"
log_info "  - Health Check: https://$SERVICE_URL/api/v1/health"
log_info "  - OpenAPI: https://$SERVICE_URL/openapi/v1.json (Development only)"
echo ""
log_info "Monitoreo:"
log_info "  - CloudWatch Logs: https://console.aws.amazon.com/cloudwatch/home?region=$AWS_REGION#logsV2:log-groups/log-group/\$252Faws\$252Fapprunner\$252F$PROJECT_NAME"
log_info "  - App Runner Console: https://console.aws.amazon.com/apprunner/home?region=$AWS_REGION#/services/$PROJECT_NAME"
echo ""
log_success "==================================================================="

# ==============================================================================
# TEST BÁSICO DEL HEALTH ENDPOINT
# ==============================================================================
log_info "Verificando health endpoint..."
sleep 5  # Dar tiempo a que el servicio esté listo

HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "https://$SERVICE_URL/api/v1/health")

if [ "$HTTP_CODE" == "200" ]; then
    log_success "Health check: OK (200) ✓"
else
    log_warning "Health check retornó: $HTTP_CODE (esperado: 200)"
    log_warning "El servicio puede estar aún inicializando. Verifica los logs."
fi

echo ""
log_success "Deployment completado!"
