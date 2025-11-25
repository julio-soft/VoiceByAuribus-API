namespace VoiceByAuribus_API.Features.WebhookSubscriptions.Application.Services;

/// <summary>
/// Service for securely managing webhook secrets using AES-256-GCM encryption.
/// Secrets are encrypted with a master key from AWS Secrets Manager.
/// </summary>
public interface IWebhookSecretService
{
    /// <summary>
    /// Encrypts a plain text secret using AES-256-GCM.
    /// </summary>
    /// <param name="plainTextSecret">The plain text secret to encrypt.</param>
    /// <returns>The encrypted secret in format: {nonce}:{ciphertext}:{tag}</returns>
    string EncryptSecret(string plainTextSecret);

    /// <summary>
    /// Decrypts an encrypted secret to retrieve the plain text.
    /// </summary>
    /// <param name="encryptedSecret">The encrypted secret to decrypt.</param>
    /// <returns>The plain text secret.</returns>
    string DecryptSecret(string encryptedSecret);

    /// <summary>
    /// Computes an HMAC-SHA256 signature for webhook payload verification.
    /// </summary>
    /// <param name="plainTextSecret">The plain text secret to use for signing.</param>
    /// <param name="payload">The payload to sign (format: "{timestamp}.{jsonBody}").</param>
    /// <returns>The hex-encoded HMAC-SHA256 signature.</returns>
    string ComputeHmacSignature(string plainTextSecret, string payload);

    /// <summary>
    /// Validates that a secret meets the security requirements.
    /// </summary>
    /// <param name="secret">The secret to validate.</param>
    /// <returns>True if the secret is valid; otherwise, false.</returns>
    bool IsValidSecret(string secret);

    /// <summary>
    /// Generates a cryptographically secure random secret (64-character hex string).
    /// </summary>
    /// <returns>A secure random secret.</returns>
    string GenerateSecret();
}
