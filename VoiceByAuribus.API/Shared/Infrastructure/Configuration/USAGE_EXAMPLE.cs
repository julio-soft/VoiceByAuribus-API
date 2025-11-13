// ============================================================================
// AWS SECRETS MANAGER CONFIGURATION PROVIDER - USAGE EXAMPLES
// ============================================================================
// This file contains examples of how to use the custom AWS Secrets Manager
// Configuration Provider in your Program.cs or Startup.cs
// ============================================================================

using VoiceByAuribus_API.Shared.Infrastructure.Configuration;
using Amazon.SecretsManager;
using Amazon;

namespace VoiceByAuribus_API.Examples;

public static class AwsSecretsManagerUsageExamples
{
    // ========================================================================
    // EXAMPLE 1: Basic Usage - Single Secret
    // ========================================================================
    public static void Example1_BasicUsage(WebApplicationBuilder builder)
    {
        // Load a single secret from AWS Secrets Manager
        builder.Configuration.AddAwsSecretsManager("myapp/production");

        // If the secret contains JSON like:
        // {
        //   "DatabasePassword": "secret123",
        //   "ApiKey": "abc-xyz"
        // }
        //
        // You can access them as:
        var dbPassword = builder.Configuration["DatabasePassword"];
        var apiKey = builder.Configuration["ApiKey"];
    }

    // ========================================================================
    // EXAMPLE 2: Environment-Specific Secrets
    // ========================================================================
    public static void Example2_EnvironmentSpecific(WebApplicationBuilder builder)
    {
        var environment = builder.Environment.EnvironmentName.ToLower();

        // Load secrets based on environment
        builder.Configuration.AddAwsSecretsManager(
            secretId: $"myapp/{environment}",
            optional: environment == "development" // Don't fail in dev if secret is missing
        );

        // Secrets structure:
        // - myapp/development (optional)
        // - myapp/staging
        // - myapp/production
    }

    // ========================================================================
    // EXAMPLE 3: Multiple Secrets
    // ========================================================================
    public static void Example3_MultipleSecrets(WebApplicationBuilder builder)
    {
        // Load multiple secrets in order
        builder.Configuration.AddAwsSecretsManager(new[]
        {
            "myapp/database",      // Database credentials
            "myapp/auth",          // Authentication settings
            "myapp/external-apis"  // Third-party API keys
        });

        // Later secrets override earlier ones if keys conflict
    }

    // ========================================================================
    // EXAMPLE 4: Key Prefix to Organize Configuration
    // ========================================================================
    public static void Example4_KeyPrefix(WebApplicationBuilder builder)
    {
        // Add prefix to all keys from this secret
        builder.Configuration.AddAwsSecretsManager(
            secretId: "myapp/database",
            keyPrefix: "Database"
        );

        // If secret contains: { "Password": "secret", "Host": "db.example.com" }
        // Access with:
        var password = builder.Configuration["Database:Password"];
        var host = builder.Configuration["Database:Host"];
    }

    // ========================================================================
    // EXAMPLE 5: Full Configuration for Production App
    // ========================================================================
    public static void Example5_ProductionConfiguration(WebApplicationBuilder builder)
    {
        var environment = builder.Environment.EnvironmentName.ToLower();

        // Base configuration from appsettings.json
        builder.Configuration
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true);

        // Load secrets from AWS Secrets Manager
        // These will override appsettings.json values
        if (environment != "development")
        {
            builder.Configuration.AddAwsSecretsManager(
                secretId: $"myapp/{environment}",
                optional: false
            );
        }

        // Environment variables have highest priority
        builder.Configuration.AddEnvironmentVariables();

        // Configuration precedence (lowest to highest):
        // 1. appsettings.json
        // 2. appsettings.{env}.json
        // 3. AWS Secrets Manager
        // 4. Environment Variables
    }

    // ========================================================================
    // EXAMPLE 6: Connection String from Secrets Manager
    // ========================================================================
    public static void Example6_ConnectionString(WebApplicationBuilder builder)
    {
        builder.Configuration.AddAwsSecretsManager("myapp/database");

        // Secret JSON format:
        // {
        //   "ConnectionStrings__DefaultConnection": "Host=db.example.com;Database=mydb;Username=user;Password=pass"
        // }
        //
        // Or using prefix:
        // {
        //   "DefaultConnection": "Host=db.example.com;..."
        // }
        // with keyPrefix: "ConnectionStrings"

        // Access:
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    }

    // ========================================================================
    // EXAMPLE 7: Custom Client for Specific Region
    // ========================================================================
    public static void Example7_CustomClient(WebApplicationBuilder builder)
    {
        builder.Configuration.AddAwsSecretsManager(source =>
        {
            source.SecretIds.Add("myapp/production");

            // Specify region explicitly
            source.SecretsManagerClientFactory = () => new AmazonSecretsManagerClient(
                new AmazonSecretsManagerConfig
                {
                    RegionEndpoint = RegionEndpoint.USEast1
                }
            );
        });
    }

    // ========================================================================
    // EXAMPLE 8: Multiple Secrets with Different Prefixes
    // ========================================================================
    public static void Example8_MultiplePrefixes(WebApplicationBuilder builder)
    {
        // Database secrets under "Database:" prefix
        builder.Configuration.AddAwsSecretsManager(
            secretId: "myapp/database",
            keyPrefix: "Database"
        );

        // Authentication secrets under "Auth:" prefix
        builder.Configuration.AddAwsSecretsManager(
            secretId: "myapp/authentication",
            keyPrefix: "Authentication"
        );

        // AWS-specific secrets under "AWS:" prefix
        builder.Configuration.AddAwsSecretsManager(
            secretId: "myapp/aws-config",
            keyPrefix: "AWS"
        );

        // Access:
        var dbPassword = builder.Configuration["Database:Password"];
        var cognitoUserPoolId = builder.Configuration["Authentication:Cognito:UserPoolId"];
        var s3BucketName = builder.Configuration["AWS:S3:BucketName"];
    }

    // ========================================================================
    // EXAMPLE 9: Real-World Secrets Manager JSON Structure
    // ========================================================================
    public static void Example9_RealWorldExample()
    {
        // Secret Name: myapp/production
        // Secret Value (JSON):
        /*
        {
          "ConnectionStrings__DefaultConnection": "Host=prod-db.example.com;Port=5432;Database=myapp;Username=appuser;Password=SuperSecret123!",

          "Authentication__Cognito__Region": "us-east-1",
          "Authentication__Cognito__UserPoolId": "us-east-1_ABC123DEF",
          "Authentication__Cognito__Audience": "voicebyauribus-api",

          "AWS__S3__AudioBucketName": "myapp-audio-files-prod",
          "AWS__S3__Region": "us-east-1",

          "AWS__SQS__PreprocessingQueueUrl": "https://sqs.us-east-1.amazonaws.com/123456789/preprocessing-queue",

          "ExternalApis__OpenAI__ApiKey": "sk-xxxxxxxxxxxxxxxx",
          "ExternalApis__Stripe__SecretKey": "sk_live_xxxxxxxxxxxxxxxx"
        }
        */

        // Usage in Program.cs:
        /*
        var builder = WebApplication.CreateBuilder(args);

        if (!builder.Environment.IsDevelopment())
        {
            builder.Configuration.AddAwsSecretsManager("myapp/production");
        }

        // Access configuration:
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        var userPoolId = builder.Configuration["Authentication:Cognito:UserPoolId"];
        var audioBucket = builder.Configuration["AWS:S3:AudioBucketName"];
        */
    }

    // ========================================================================
    // EXAMPLE 10: Testing with Mock Client
    // ========================================================================
    public static void Example10_TestingWithMock(WebApplicationBuilder builder)
    {
        // For integration tests, inject a mock client
        var mockClient = CreateMockSecretsManagerClient(); // Your mock implementation

        builder.Configuration.AddAwsSecretsManager(source =>
        {
            source.SecretIds.Add("test/secrets");
            source.SecretsManagerClientFactory = () => mockClient;
            source.Optional = true; // Don't fail tests if something goes wrong
        });
    }

    private static IAmazonSecretsManager CreateMockSecretsManagerClient()
    {
        // Return your mock/stub implementation
        throw new System.NotImplementedException("Implement your mock");
    }
}
