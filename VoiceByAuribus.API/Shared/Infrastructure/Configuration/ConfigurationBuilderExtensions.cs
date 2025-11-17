using System;
using System.Collections.Generic;
using Amazon.SecretsManager;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace VoiceByAuribus_API.Shared.Infrastructure.Configuration;

/// <summary>
/// Extension methods for IConfigurationBuilder to add AWS Secrets Manager support
/// </summary>
public static class ConfigurationBuilderExtensions
{
    /// <summary>
    /// Adds AWS Secrets Manager as a configuration source
    /// </summary>
    /// <param name="builder">The configuration builder</param>
    /// <param name="secretId">The secret ID to load</param>
    /// <param name="optional">If true, missing secrets won't throw exceptions</param>
    /// <param name="keyPrefix">Optional prefix to add to all configuration keys</param>
    /// <param name="region">AWS Region (e.g., "us-east-1"). If not specified, uses AWS SDK default region discovery</param>
    /// <returns>The configuration builder for chaining</returns>
    public static IConfigurationBuilder AddAwsSecretsManager(
        this IConfigurationBuilder builder,
        string secretId,
        bool optional = false,
        string? keyPrefix = null,
        string? region = null)
    {
        return builder.AddAwsSecretsManager(new[] { secretId }, optional, keyPrefix, region);
    }

    /// <summary>
    /// Adds AWS Secrets Manager as a configuration source with multiple secrets
    /// </summary>
    /// <param name="builder">The configuration builder</param>
    /// <param name="secretIds">The secret IDs to load</param>
    /// <param name="optional">If true, missing secrets won't throw exceptions</param>
    /// <param name="keyPrefix">Optional prefix to add to all configuration keys</param>
    /// <param name="region">AWS Region (e.g., "us-east-1"). If not specified, uses AWS SDK default region discovery</param>
    /// <returns>The configuration builder for chaining</returns>
    public static IConfigurationBuilder AddAwsSecretsManager(
        this IConfigurationBuilder builder,
        IEnumerable<string> secretIds,
        bool optional = false,
        string? keyPrefix = null,
        string? region = null)
    {
        return builder.AddAwsSecretsManager(source =>
        {
            source.SecretIds.AddRange(secretIds);
            source.Optional = optional;
            source.KeyPrefix = keyPrefix;
            source.Region = region;
        });
    }

    /// <summary>
    /// Adds AWS Secrets Manager as a configuration source with full customization
    /// </summary>
    /// <param name="builder">The configuration builder</param>
    /// <param name="configureSource">Action to configure the source</param>
    /// <returns>The configuration builder for chaining</returns>
    public static IConfigurationBuilder AddAwsSecretsManager(
        this IConfigurationBuilder builder,
        Action<AwsSecretsManagerConfigurationSource> configureSource)
    {
        var source = new AwsSecretsManagerConfigurationSource();
        configureSource(source);

        return builder.Add(source);
    }

    /// <summary>
    /// Adds AWS Secrets Manager as a configuration source with custom client
    /// </summary>
    /// <param name="builder">The configuration builder</param>
    /// <param name="secretId">The secret ID to load</param>
    /// <param name="clientFactory">Factory to create IAmazonSecretsManager client</param>
    /// <param name="optional">If true, missing secrets won't throw exceptions</param>
    /// <param name="keyPrefix">Optional prefix to add to all configuration keys</param>
    /// <returns>The configuration builder for chaining</returns>
    public static IConfigurationBuilder AddAwsSecretsManager(
        this IConfigurationBuilder builder,
        string secretId,
        Func<IAmazonSecretsManager> clientFactory,
        bool optional = false,
        string? keyPrefix = null)
    {
        return builder.AddAwsSecretsManager(source =>
        {
            source.SecretIds.Add(secretId);
            source.SecretsManagerClientFactory = clientFactory;
            source.Optional = optional;
            source.KeyPrefix = keyPrefix;
        });
    }
}
