using System;
using System.Collections.Generic;
using Amazon.SecretsManager;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace VoiceByAuribus_API.Shared.Infrastructure.Configuration;

/// <summary>
/// Configuration source for AWS Secrets Manager
/// </summary>
public class AwsSecretsManagerConfigurationSource : IConfigurationSource
{
    /// <summary>
    /// List of secret IDs to load from AWS Secrets Manager
    /// </summary>
    public List<string> SecretIds { get; } = new();

    /// <summary>
    /// Optional prefix to add to all configuration keys
    /// </summary>
    public string? KeyPrefix { get; set; }

    /// <summary>
    /// If true, errors loading secrets will be logged but not thrown
    /// </summary>
    public bool Optional { get; set; }

    /// <summary>
    /// Factory function to create IAmazonSecretsManager client (for testing/customization)
    /// </summary>
    public Func<IAmazonSecretsManager>? SecretsManagerClientFactory { get; set; }

    /// <summary>
    /// Logger for diagnostics
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// Enable console logging when ILogger is not available (default: true)
    /// </summary>
    public bool EnableConsoleLogging { get; set; } = true;

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new AwsSecretsManagerConfigurationProvider(this, Logger);
    }
}
