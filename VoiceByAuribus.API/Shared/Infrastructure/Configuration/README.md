# AWS Secrets Manager Configuration Provider

Custom ASP.NET Core Configuration Provider for loading secrets from AWS Secrets Manager without third-party dependencies.

## Features

- ✅ Uses official AWS SDK v4 (`AWSSDK.SecretsManager`)
- ✅ Integrates seamlessly with ASP.NET Core Configuration system
- ✅ Supports JSON secrets (automatically parsed as key-value pairs)
- ✅ Supports plain text secrets
- ✅ Multiple secrets support
- ✅ Optional secrets (won't fail if missing)
- ✅ Key prefix support for organizing configuration
- ✅ Custom client factory support (for testing/customization)

## Usage

### Basic Usage

Add to your `Program.cs` or `Startup.cs`:

```csharp
using VoiceByAuribus_API.Shared.Infrastructure.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add AWS Secrets Manager as configuration source
builder.Configuration.AddAwsSecretsManager("my-app/production");

// Now you can access secrets like normal configuration
var dbPassword = builder.Configuration["DatabasePassword"];
```

### Loading JSON Secrets

If your secret contains JSON:

```json
{
  "DatabasePassword": "mySecretPassword",
  "ApiKey": "abc123xyz",
  "ConnectionStrings__DefaultConnection": "Host=db.example.com;Database=mydb"
}
```

They will be automatically parsed and available as:

```csharp
var dbPassword = builder.Configuration["DatabasePassword"];
var apiKey = builder.Configuration["ApiKey"];
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
```

### Multiple Secrets

```csharp
builder.Configuration.AddAwsSecretsManager(new[]
{
    "my-app/database",
    "my-app/api-keys",
    "my-app/aws-config"
});
```

### Optional Secrets

If a secret might not exist (e.g., in development):

```csharp
builder.Configuration.AddAwsSecretsManager(
    secretId: "my-app/optional-config",
    optional: true
);
```

### Key Prefix

Add a prefix to all keys from a secret:

```csharp
builder.Configuration.AddAwsSecretsManager(
    secretId: "my-app/database",
    optional: false,
    keyPrefix: "Database"
);

// If secret contains: { "Password": "secret" }
// Access with: builder.Configuration["Database:Password"]
```

### Advanced Configuration

```csharp
builder.Configuration.AddAwsSecretsManager(source =>
{
    source.SecretIds.Add("my-app/production");
    source.SecretIds.Add("my-app/shared");
    source.Optional = false;
    source.KeyPrefix = "App";

    // Custom client (useful for testing or specific regions)
    source.SecretsManagerClientFactory = () => new AmazonSecretsManagerClient(
        new AmazonSecretsManagerConfig { RegionEndpoint = RegionEndpoint.USEast1 }
    );
});
```

### Environment-Specific Secrets

```csharp
var environment = builder.Environment.EnvironmentName;

builder.Configuration.AddAwsSecretsManager(
    secretId: $"my-app/{environment.ToLower()}",
    optional: environment == "Development"
);
```

## Secret Formats

### JSON Secret (Recommended)

```json
{
  "DatabasePassword": "myPassword123",
  "ApiKey": "abc-def-ghi",
  "ConnectionStrings__DefaultConnection": "Host=localhost;Database=mydb",
  "Authentication__Cognito__UserPoolId": "us-east-1_ABC123"
}
```

### Plain Text Secret

For plain text secrets, the entire value is stored under the `keyPrefix` (if specified) or the secret ID:

```csharp
// With prefix
builder.Configuration.AddAwsSecretsManager("my-secret", keyPrefix: "MySecret");
var value = builder.Configuration["MySecret"]; // Gets the plain text value

// Without prefix
builder.Configuration.AddAwsSecretsManager("my-secret");
var value = builder.Configuration["my-secret"]; // Gets the plain text value
```

## Best Practices

1. **Use JSON format** for multiple related values
2. **Use hierarchical keys** with `:` or `__` separators (e.g., `Database:Password`)
3. **Set optional: true** for development/local environments
4. **Use IAM roles** for authentication (EC2, ECS, Lambda, App Runner)
5. **Organize secrets by environment**: `myapp/dev`, `myapp/staging`, `myapp/prod`
6. **Use key prefixes** to avoid naming conflicts

## IAM Permissions Required

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "secretsmanager:GetSecretValue"
      ],
      "Resource": "arn:aws:secretsmanager:REGION:ACCOUNT:secret:my-app/*"
    }
  ]
}
```

## Testing

For unit/integration tests, provide a mock client:

```csharp
builder.Configuration.AddAwsSecretsManager(source =>
{
    source.SecretIds.Add("test-secret");
    source.SecretsManagerClientFactory = () => mockSecretsManagerClient;
});
```

## Error Handling

- **Secret not found**: Throws `ResourceNotFoundException` (unless `optional: true`)
- **Access denied**: Throws `AccessDeniedException`
- **Other AWS errors**: Logged and thrown (unless `optional: true`)

## Implementation Details

This provider:
- Loads secrets **synchronously at startup** (blocks until loaded)
- Does **not reload** secrets automatically (restart required for updates)
- Parses JSON secrets into hierarchical configuration keys
- Falls back to plain text if JSON parsing fails
- Integrates with ASP.NET Core's configuration system (precedence rules apply)

## Configuration Precedence

Secrets Manager values will override previous configuration sources and be overridden by subsequent ones:

```csharp
builder.Configuration
    .AddJsonFile("appsettings.json")           // Lowest priority
    .AddAwsSecretsManager("my-app/config")     // Overrides JSON file
    .AddEnvironmentVariables();                 // Highest priority (overrides secrets)
```
