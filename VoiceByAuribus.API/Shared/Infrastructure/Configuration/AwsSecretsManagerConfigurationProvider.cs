using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace VoiceByAuribus_API.Shared.Infrastructure.Configuration;

/// <summary>
/// Configuration provider that loads secrets from AWS Secrets Manager
/// </summary>
public class AwsSecretsManagerConfigurationProvider : ConfigurationProvider
{
    private readonly AwsSecretsManagerConfigurationSource _source;
    private readonly ILogger? _logger;

    public AwsSecretsManagerConfigurationProvider(
        AwsSecretsManagerConfigurationSource source,
        ILogger? logger = null)
    {
        _source = source;
        _logger = logger;
    }

    public override void Load()
    {
        LoadAsync().GetAwaiter().GetResult();
    }

    private async Task LoadAsync()
    {
        try
        {
            using var client = _source.SecretsManagerClientFactory?.Invoke()
                ?? new AmazonSecretsManagerClient();

            foreach (var secretId in _source.SecretIds)
            {
                try
                {
                    var request = new GetSecretValueRequest { SecretId = secretId };
                    var response = await client.GetSecretValueAsync(request);

                    if (!string.IsNullOrWhiteSpace(response.SecretString))
                    {
                        ParseSecret(secretId, response.SecretString);
                    }
                }
                catch (ResourceNotFoundException)
                {
                    _logger?.LogWarning("Secret '{SecretId}' not found in AWS Secrets Manager", secretId);
                    if (!_source.Optional)
                    {
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error loading secret '{SecretId}' from AWS Secrets Manager", secretId);
                    if (!_source.Optional)
                    {
                        throw;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error initializing AWS Secrets Manager client");
            if (!_source.Optional)
            {
                throw;
            }
        }
    }

    private void ParseSecret(string secretId, string secretString)
    {
        try
        {
            // Try to parse as JSON first
            var secretData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(secretString);

            if (secretData != null)
            {
                foreach (var kvp in secretData)
                {
                    var key = _source.KeyPrefix != null
                        ? $"{_source.KeyPrefix}:{kvp.Key}"
                        : kvp.Key;

                    var value = kvp.Value.ValueKind == JsonValueKind.String
                        ? kvp.Value.GetString()
                        : kvp.Value.ToString();

                    if (value != null)
                    {
                        Set(key, value);
                    }
                }
            }
        }
        catch (JsonException)
        {
            // If not JSON, store the entire secret as a single value
            var key = _source.KeyPrefix != null
                ? _source.KeyPrefix
                : secretId;

            Set(key, secretString);
        }
    }
}
