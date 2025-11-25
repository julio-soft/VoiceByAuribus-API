using System.Security.Cryptography;
using System.Text;
using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Features.WebhookSubscriptions.Application.Services;

/// <summary>
/// Service for securely managing webhook secrets using AES-256-GCM encryption and HMAC-SHA256 signatures.
/// Secrets are encrypted using a master key from AWS Secrets Manager.
/// </summary>
public class WebhookSecretService(IEncryptionService encryptionService) : IWebhookSecretService
{
    private const int MinSecretLength = 32;

    /// <inheritdoc />
    public string EncryptSecret(string plainTextSecret)
    {
        if (string.IsNullOrWhiteSpace(plainTextSecret))
        {
            throw new ArgumentException("Secret cannot be null or whitespace", nameof(plainTextSecret));
        }

        if (!IsValidSecret(plainTextSecret))
        {
            throw new ArgumentException($"Secret must be at least {MinSecretLength} characters long", nameof(plainTextSecret));
        }

        return encryptionService.Encrypt(plainTextSecret);
    }

    /// <inheritdoc />
    public string DecryptSecret(string encryptedSecret)
    {
        if (string.IsNullOrWhiteSpace(encryptedSecret))
        {
            throw new ArgumentException("Encrypted secret cannot be null or whitespace", nameof(encryptedSecret));
        }

        return encryptionService.Decrypt(encryptedSecret);
    }

    /// <inheritdoc />
    public string ComputeHmacSignature(string plainTextSecret, string payload)
    {
        if (string.IsNullOrWhiteSpace(plainTextSecret))
        {
            throw new ArgumentException("Secret cannot be null or whitespace", nameof(plainTextSecret));
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException("Payload cannot be null or whitespace", nameof(payload));
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(plainTextSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <inheritdoc />
    public bool IsValidSecret(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        // Must be at least 32 characters for security
        return secret.Length >= MinSecretLength;
    }

    /// <inheritdoc />
    public string GenerateSecret()
    {
        // Generate cryptographically secure 32-byte (256-bit) secret
        // Encoded as 64-character hex string
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
