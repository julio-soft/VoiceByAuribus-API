# VoiceByAuribus.API.Tests

Proyecto de testing para VoiceByAuribus API siguiendo la estrategia definida en [TESTING_STRATEGY.md](./TESTING_STRATEGY.md).

## 🏗️ Estructura del Proyecto

```
VoiceByAuribus.API.Tests/
├── Unit/                    # Tests unitarios (50-60% cobertura)
│   ├── Features/           # Tests por feature (Vertical Slice)
│   │   ├── AudioFiles/
│   │   ├── VoiceConversions/
│   │   ├── WebhookSubscriptions/
│   │   └── Voices/
│   └── Shared/             # Tests de servicios compartidos
│       ├── Services/
│       └── Middleware/
│
├── Integration/             # Tests de integración (30-40% cobertura)
│   ├── Features/           # Controllers + Services + DB
│   └── Infrastructure/     # DbContext, Auth, etc.
│
├── E2E/                     # Tests end-to-end (5-10% cobertura)
│
└── Helpers/                 # Utilities para testing
    ├── MockServices/       # Mocks de servicios externos
    └── Builders/           # Test data builders
```

## 🚀 Comandos Rápidos

```bash
# Ejecutar todos los tests
dotnet test

# Solo unit tests
dotnet test --filter "FullyQualifiedName~Unit"

# Solo integration tests
dotnet test --filter "FullyQualifiedName~Integration"

# Solo E2E tests
dotnet test --filter "FullyQualifiedName~E2E"

# Con cobertura de código
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Watch mode (re-ejecuta al cambiar código)
dotnet watch test
```

## 📦 Paquetes Instalados

### Testing Framework
- **xUnit**: Framework de testing
- **FluentAssertions**: Assertions legibles
- **Moq**: Mocking framework

### Integration Tests
- **WebApplicationFactory**: Test server
- **Testcontainers**: PostgreSQL containers
- **Respawn**: Database cleanup

### Test Data
- **AutoFixture**: Generación de datos
- **Bogus**: Datos falsos realistas

### Mock External Services
- **WireMock.Net**: Mock de APIs HTTP

## 📋 Convenciones de Naming

### Nombres de Tests
```csharp
// Patrón: MethodName_StateUnderTest_ExpectedBehavior
[Fact]
public void CreateVoiceConversion_WithValidData_ReturnsCreatedConversion() { }

[Fact]
public void CreateVoiceConversion_WithInvalidAudioFile_ThrowsException() { }
```

### Archivos de Test
```
{ClassUnderTest}Tests.cs
Ejemplo: VoiceConversionServiceTests.cs
```

## 🎯 Patrón AAA (Arrange-Act-Assert)

Todos los tests siguen el patrón AAA:

```csharp
[Fact]
public async Task ExampleTest()
{
    // Arrange: Preparar datos y mocks
    var mockService = new Mock<ISomeService>();
    mockService.Setup(x => x.MethodAsync()).ReturnsAsync(expectedResult);
    
    // Act: Ejecutar el método bajo prueba
    var result = await systemUnderTest.MethodAsync();
    
    // Assert: Verificar resultados
    result.Should().NotBeNull();
    result.Should().Be(expectedResult);
}
```

## 📊 Objetivos de Cobertura

- **Unit Tests**: 80%+ en Services, Validators, Helpers
- **Integration Tests**: 70%+ en Controllers, Background Services
- **Global**: 75%+ cobertura total

## 🔗 Referencias

- [Estrategia de Testing Completa](./TESTING_STRATEGY.md)
- [Copilot Instructions](../.github/copilot-instructions.md)
- [xUnit Documentation](https://xunit.net/)
- [FluentAssertions Documentation](https://fluentassertions.com/)
- [Moq Documentation](https://github.com/moq/moq4)
- [Testcontainers Documentation](https://dotnet.testcontainers.org/)
