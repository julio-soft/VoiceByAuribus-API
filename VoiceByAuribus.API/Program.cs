using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using VoiceByAuribus_API.Features.Auth;
using VoiceByAuribus_API.Features.Voices;
using VoiceByAuribus_API.Features.AudioFiles;
using VoiceByAuribus_API.Features.Auth.Presentation;
using VoiceByAuribus_API.Infrastructure.DependencyInjection;
using VoiceByAuribus_API.Shared.Infrastructure.Middleware;
using VoiceByAuribus_API.Shared.Infrastructure.Services;
using VoiceByAuribus_API.Shared.Infrastructure.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Load secrets from AWS Secrets Manager in production
LoadSecretsInProduction(builder);

builder.Services.AddAuthFeature();
builder.Services.AddVoicesFeature();
builder.Services.AddAudioFilesFeature();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services
    .AddControllers(options =>
    {
        options.Filters.Add<VoiceByAuribus_API.Shared.Infrastructure.Filters.ValidationFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});

builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();

ConfigureAuthentication(builder);
ConfigureAuthorization(builder);

var app = builder.Build();

// Global exception handler (must be first)
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

/// <summary>
/// Loads secrets from AWS Secrets Manager based on the current environment.
///
/// Secret naming convention: voice-by-auribus-api/{environment}
/// Examples:
/// - voice-by-auribus-api/production
/// - voice-by-auribus-api/staging
/// - voice-by-auribus-api/development (optional)
///
/// The secret should contain a JSON object with all sensitive configuration values.
/// See SECRETS_STRUCTURE.md for the expected JSON format.
/// </summary>
static void LoadSecretsInProduction(WebApplicationBuilder builder)
{
    var environment = builder.Environment.EnvironmentName.ToLower();

    // In Development, secrets are optional (use appsettings.json)
    // In Production/Staging, secrets are required
    var secretsRequired = !builder.Environment.IsDevelopment();

    // Secret name following the convention: voice-by-auribus-api/{environment}
    var secretId = $"voice-by-auribus-api/{environment}";

    // Load secrets from AWS Secrets Manager
    builder.Configuration.AddAwsSecretsManager(
        secretId: secretId,
        optional: !secretsRequired
    );

    // Log the configuration source for debugging
    if (secretsRequired)
    {
        Console.WriteLine($"[Secrets Manager] Loading secrets from: {secretId}");
    }
    else
    {
        Console.WriteLine($"[Secrets Manager] Secrets are optional in Development. Using appsettings.json if secret '{secretId}' is not found.");
    }
}

static void ConfigureAuthentication(WebApplicationBuilder builder)
{
    var cognitoSection = builder.Configuration.GetSection("Authentication:Cognito");
    var region = cognitoSection.GetValue<string>("Region") ?? throw new InvalidOperationException("Authentication:Cognito:Region configuration is required.");
    var userPoolId = cognitoSection.GetValue<string>("UserPoolId") ?? throw new InvalidOperationException("Authentication:Cognito:UserPoolId configuration is required.");
    var audience = cognitoSection.GetValue<string>("Audience") ?? throw new InvalidOperationException("Authentication:Cognito:Audience configuration is required.");

    var authority = $"https://cognito-idp.{region}.amazonaws.com/{userPoolId}";

    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.Authority = authority;
            options.RequireHttpsMetadata = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = authority,
                ValidateAudience = false, // M2M tokens don't include 'aud' claim
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true
            };

            // For Cognito M2M tokens, validate that required scopes are present
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    var scopeClaim = context.Principal?.FindFirst("scope")?.Value;
                    if (string.IsNullOrWhiteSpace(scopeClaim))
                    {
                        context.Fail("Missing required scope claim");
                        return Task.CompletedTask;
                    }

                    // Verify at least one valid resource server scope is present
                    var scopes = scopeClaim.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var hasValidScope = scopes.Any(s => s.StartsWith($"{audience}/", StringComparison.OrdinalIgnoreCase));

                    if (!hasValidScope)
                    {
                        context.Fail($"Token must contain at least one scope for resource '{audience}'");
                    }

                    return Task.CompletedTask;
                }
            };
        });
}

static void ConfigureAuthorization(WebApplicationBuilder builder)
{
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy(AuthorizationPolicies.Base, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context =>
                HasScope(context.User, AuthorizationScopes.Base) ||
                HasScope(context.User, AuthorizationScopes.Admin));
        });

        options.AddPolicy(AuthorizationPolicies.Admin, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context => HasScope(context.User, AuthorizationScopes.Admin));
        });
    });

    static bool HasScope(System.Security.Claims.ClaimsPrincipal user, string requiredScope)
    {
        var scopeValues = user.FindAll("scope")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        if (scopeValues.Any(scope => scope.Equals(requiredScope, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return user.FindAll("cognito:groups")
            .Any(group => group.Value.Equals(requiredScope, StringComparison.OrdinalIgnoreCase));
    }
}
