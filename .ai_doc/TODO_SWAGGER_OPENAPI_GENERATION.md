# TODO: Configure Automatic OpenAPI Generation with Swashbuckle

**Priority**: Medium
**Status**: Pending
**Created**: 2025-11-25

## Overview

Currently, the OpenAPI specification for the documentation site (`docs-site/openapi/voicebyauribus-api.yaml`) is maintained manually. This should be automated using Swashbuckle.AspNetCore to generate it directly from the .NET API code.

## Benefits

- ✅ **Auto-synchronized**: Spec always matches the actual API implementation
- ✅ **Automatic updates**: Changes to endpoints/DTOs automatically reflect in docs
- ✅ **Validation included**: All FluentValidation and DataAnnotations are included
- ✅ **Reduced maintenance**: No manual YAML editing required
- ✅ **Filtered endpoints**: Can exclude admin endpoints from public docs automatically

## Implementation Steps

### 1. Install Swashbuckle Package

```bash
cd VoiceByAuribus.API
dotnet add package Swashbuckle.AspNetCore
dotnet add package Swashbuckle.AspNetCore.Annotations
```

### 2. Configure Program.cs

Add before `builder.Build()`:

```csharp
// Add Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "VoiceByAuribus API",
        Version = "v1",
        Description = "Professional Voice Conversion and Audio Processing API",
        Contact = new OpenApiContact
        {
            Name = "Auribus Support",
            Email = "support@auribus.io",
            Url = new Uri("https://auribus.io")
        }
    });

    // Add JWT authentication definition
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "OAuth 2.0 Bearer token obtained from the authentication server",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Enable XML comments
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }

    // Filter to exclude admin endpoints
    c.DocumentFilter<ExcludeAdminEndpointsFilter>();

    // Use snake_case for JSON properties
    c.DescribeAllParametersInCamelCase();
});
```

Add after `var app = builder.Build()`:

```csharp
// Enable Swagger middleware
if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "VoiceByAuribus API v1");
        c.RoutePrefix = "swagger"; // Access at /swagger
    });
}
```

### 3. Create Admin Endpoints Filter

Create file: `Shared/Infrastructure/Filters/ExcludeAdminEndpointsFilter.cs`

```csharp
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace VoiceByAuribus.API.Shared.Infrastructure.Filters;

/// <summary>
/// Swagger document filter to exclude admin-only endpoints from public API documentation
/// </summary>
public class ExcludeAdminEndpointsFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var pathsToRemove = swaggerDoc.Paths
            .Where(path =>
                // Exclude paths that require admin scope
                context.ApiDescriptions
                    .Any(api =>
                        api.RelativePath == path.Key.TrimStart('/') &&
                        api.ActionDescriptor.EndpointMetadata
                            .OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
                            .Any(auth => auth.Policy?.Contains("Admin") == true)
                    )
            )
            .Select(x => x.Key)
            .ToList();

        foreach (var path in pathsToRemove)
        {
            swaggerDoc.Paths.Remove(path);
        }
    }
}
```

### 4. Enable XML Documentation

Update `VoiceByAuribus-API.csproj`:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <NoWarn>$(NoWarn);1591</NoWarn>
</PropertyGroup>
```

### 5. Add XML Comments to Controllers

Example:

```csharp
/// <summary>
/// Retrieves all available voice models
/// </summary>
/// <returns>List of voice models</returns>
/// <response code="200">Successfully retrieved voice models</response>
/// <response code="401">Unauthorized - Invalid or missing authentication token</response>
[HttpGet]
[ProducesResponseType(typeof(ApiResponse<List<VoiceResponseDto>>), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public async Task<IActionResult> GetVoices()
{
    // ...
}
```

### 6. Generate OpenAPI Spec File

After running the API:

```bash
# Start API in development mode
cd VoiceByAuribus.API
dotnet run

# In another terminal, download the spec
curl http://localhost:5037/swagger/v1/swagger.json > ../docs-site/openapi/voicebyauribus-api.json

# OR convert to YAML (requires yq or similar tool)
curl http://localhost:5037/swagger/v1/swagger.json | yq eval -P - > ../docs-site/openapi/voicebyauribus-api.yaml
```

### 7. Create Generation Script

Create file: `scripts/generate-openapi-spec.sh`

```bash
#!/bin/bash

# Generate OpenAPI specification from running API

API_URL="http://localhost:5037"
OUTPUT_DIR="docs-site/openapi"

echo "Generating OpenAPI specification..."

# Check if API is running
if ! curl -f -s "$API_URL/health" > /dev/null; then
    echo "Error: API is not running at $API_URL"
    echo "Please start the API first: cd VoiceByAuribus.API && dotnet run"
    exit 1
fi

# Download OpenAPI JSON
curl -s "$API_URL/swagger/v1/swagger.json" > "$OUTPUT_DIR/voicebyauribus-api.json"

# Convert to YAML (if yq is installed)
if command -v yq &> /dev/null; then
    yq eval -P "$OUTPUT_DIR/voicebyauribus-api.json" > "$OUTPUT_DIR/voicebyauribus-api.yaml"
    echo "✅ OpenAPI spec generated in JSON and YAML formats"
else
    echo "⚠️  OpenAPI spec generated in JSON format only"
    echo "   Install yq to generate YAML: brew install yq"
fi

echo "✅ OpenAPI specification updated at $OUTPUT_DIR/"
```

Make executable:
```bash
chmod +x scripts/generate-openapi-spec.sh
```

### 8. Update Docusaurus Config

If using JSON instead of YAML:

```typescript
// docusaurus.config.ts
config: {
  voicebyauribus: {
    specPath: "openapi/voicebyauribus-api.json", // Changed from .yaml
    outputDir: "docs/api",
    // ...
  },
}
```

## Alternative: Swashbuckle CLI

Can also generate without running the API:

```bash
dotnet tool install -g Swashbuckle.AspNetCore.Cli
dotnet swagger tofile --output ../docs-site/openapi/voicebyauribus-api.json VoiceByAuribus-API.csproj v1
```

## Workflow Integration

### Development Workflow

1. Make changes to API (add/modify endpoints)
2. Add XML documentation comments
3. Run generation script: `./scripts/generate-openapi-spec.sh`
4. Commit both API changes and updated OpenAPI spec
5. Documentation site auto-updates

### CI/CD Integration

Add to GitHub Actions workflow:

```yaml
- name: Generate OpenAPI Spec
  run: |
    cd VoiceByAuribus.API
    dotnet run &
    sleep 10  # Wait for API to start
    cd ..
    ./scripts/generate-openapi-spec.sh

- name: Commit updated OpenAPI spec
  run: |
    git config --local user.email "github-actions[bot]@users.noreply.github.com"
    git config --local user.name "github-actions[bot]"
    git add docs-site/openapi/
    git diff --quiet && git diff --staged --quiet || git commit -m "docs: Update OpenAPI specification"
```

## References

- [Swashbuckle Documentation](https://github.com/domaindrivendev/Swashbuckle.AspNetCore)
- [OpenAPI Specification](https://swagger.io/specification/)
- [ASP.NET Core OpenAPI](https://learn.microsoft.com/en-us/aspnet/core/tutorials/web-api-help-pages-using-swagger)

## Notes

- Currently using manual YAML file at `docs-site/openapi/voicebyauribus-api.yaml`
- This manual file should be replaced once Swagger is configured
- Keep the manual file as backup until Swagger generation is tested
- Consider adding Swagger annotations to DTOs for better documentation
- Use `[SwaggerIgnore]` attribute to hide sensitive properties

## Estimated Effort

- **Implementation**: 2-3 hours
- **Testing**: 1 hour
- **Documentation**: 30 minutes

**Total**: ~4 hours
