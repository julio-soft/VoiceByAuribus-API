using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon;
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
        // Note: Using GetAwaiter().GetResult() is generally not recommended, but is safe
        // in the context of application startup before the app is built
        LoadAsync().GetAwaiter().GetResult();
    }

    private async Task LoadAsync()
    {
        var loadedCount = 0;
        const int maxRetries = 5;
        const int initialDelayMs = 1000; // 1 second

        try
        {
            // Retry logic for App Runner - credentials may take a few seconds to be available
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    if (attempt > 0)
                    {
                        var delayMs = initialDelayMs * (int)Math.Pow(2, attempt - 1); // Exponential backoff
                        LogInformation($"Retrying to load secrets (attempt {attempt + 1}/{maxRetries}) after {delayMs}ms...");
                        await Task.Delay(delayMs);
                    }

                    using var client = _source.SecretsManagerClientFactory?.Invoke()
                        ?? CreateSecretsManagerClient();

                    foreach (var secretId in _source.SecretIds)
                    {
                        try
                        {
                            LogInformation($"Loading secret: {secretId}");

                            var request = new GetSecretValueRequest { SecretId = secretId };
                            var response = await client.GetSecretValueAsync(request);

                            if (!string.IsNullOrWhiteSpace(response.SecretString))
                            {
                                ParseSecret(secretId, response.SecretString);
                                loadedCount++;
                                LogInformation($"Successfully loaded secret: {secretId}");
                            }
                            else
                            {
                                LogWarning($"Secret '{secretId}' is empty");
                            }
                        }
                        catch (ResourceNotFoundException)
                        {
                            var message = $"Secret '{secretId}' not found in AWS Secrets Manager";
                            LogWarning(message);

                            if (!_source.Optional)
                            {
                                throw new InvalidOperationException(message);
                            }
                        }
                        catch (Exception ex)
                        {
                            var message = $"Error loading secret '{secretId}' from AWS Secrets Manager: {ex.Message}";
                            LogError(message, ex);

                            if (!_source.Optional)
                            {
                                throw;
                            }
                        }
                    }

                    if (loadedCount > 0)
                    {
                        LogInformation($"Successfully loaded {loadedCount} secret(s) from AWS Secrets Manager");
                        break; // Success - exit retry loop
                    }
                    else if (_source.SecretIds.Count > 0 && !_source.Optional)
                    {
                        throw new InvalidOperationException("No secrets were loaded and secrets are required");
                    }
                }
                catch (Exception ex)
                {
                    var isLastAttempt = attempt == maxRetries - 1;
                    var message = $"Error initializing AWS Secrets Manager client (attempt {attempt + 1}/{maxRetries}): {ex.Message}";

                    if (isLastAttempt)
                    {
                        LogError(message, ex);
                        LogError("CRITICAL: Failed to load secrets after all retry attempts", ex);

                        if (!_source.Optional)
                        {
                            throw;
                        }
                    }
                    else
                    {
                        LogWarning(message);
                        // Continue to next retry
                    }
                }
            }
        }
        catch (Exception ex)
        {
            var message = "CRITICAL: Fatal error loading secrets from AWS Secrets Manager";
            LogError(message, ex);

            if (!_source.Optional)
            {
                throw;
            }
        }
    }

    private void LogInformation(string message)
    {
        _logger?.LogInformation(message);
        if (_logger == null && _source.EnableConsoleLogging)
        {
            Console.WriteLine($"[Secrets Manager] {message}");
            Console.Out.Flush();
        }
    }

    private void LogWarning(string message)
    {
        _logger?.LogWarning(message);
        if (_logger == null && _source.EnableConsoleLogging)
        {
            Console.WriteLine($"[Secrets Manager WARNING] {message}");
            Console.Out.Flush();
        }
    }

    private void LogError(string message, Exception? ex = null)
    {
        _logger?.LogError(ex, message);
        if (_logger == null && _source.EnableConsoleLogging)
        {
            Console.WriteLine($"[Secrets Manager ERROR] {message}");
            if (ex != null)
            {
                Console.WriteLine($"[Secrets Manager ERROR] Exception: {ex.GetType().Name}: {ex.Message}");
            }
            Console.Out.Flush();
        }
    }

    private IAmazonSecretsManager CreateSecretsManagerClient()
    {
        if (!string.IsNullOrWhiteSpace(_source.Region))
        {
            var regionEndpoint = RegionEndpoint.GetBySystemName(_source.Region);
            LogInformation($"Creating Secrets Manager client for region: {_source.Region}");
            return new AmazonSecretsManagerClient(regionEndpoint);
        }

        LogInformation("Creating Secrets Manager client with default configuration");
        return new AmazonSecretsManagerClient();
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
                    // Convert double underscore (__) to colon (:) for ASP.NET Core Configuration hierarchy
                    // Example: "ConnectionStrings__DefaultConnection" -> "ConnectionStrings:DefaultConnection"
                    var normalizedKey = kvp.Key.Replace("__", ":");

                    var key = _source.KeyPrefix != null
                        ? $"{_source.KeyPrefix}:{normalizedKey}"
                        : normalizedKey;

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
