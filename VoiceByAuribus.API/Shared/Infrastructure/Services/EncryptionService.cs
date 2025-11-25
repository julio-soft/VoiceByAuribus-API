using System;
using System.Security.Cryptography;
using System.Text;
using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Shared.Infrastructure.Services;

/// <summary>
/// Service for encrypting and decrypting sensitive data using AES-256-GCM.
/// Uses a master key from AWS Secrets Manager configuration.
///
/// Configuration required in AWS Secrets Manager JSON:
/// {
///   "Encryption": {
///     "MasterKey": "base64-encoded-32-byte-key"
///   }
/// }
///
/// To generate a new master key:
/// openssl rand -base64 32
/// </summary>
public class EncryptionService : IEncryptionService
{
    private readonly byte[] _masterKey;
    private readonly ILogger<EncryptionService> _logger;

    public EncryptionService(IConfiguration configuration, ILogger<EncryptionService> logger)
    {
        _logger = logger;

        // Load master key from configuration (should come from AWS Secrets Manager)
        var masterKeyBase64 = configuration.GetValue<string>("Encryption:MasterKey");

        if (string.IsNullOrWhiteSpace(masterKeyBase64))
        {
            throw new InvalidOperationException(
                "Encryption master key not configured. " +
                "Add 'Encryption:MasterKey' to AWS Secrets Manager JSON. " +
                "Generate with: openssl rand -base64 32");
        }

        try
        {
            _masterKey = Convert.FromBase64String(masterKeyBase64);

            if (_masterKey.Length != 32)
            {
                throw new InvalidOperationException(
                    $"Invalid master key length: {_masterKey.Length} bytes. " +
                    "AES-256 requires 32 bytes. " +
                    "Generate with: openssl rand -base64 32");
            }

            _logger.LogInformation("Encryption service initialized with AES-256-GCM");
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "Invalid master key format. Must be base64-encoded. " +
                "Generate with: openssl rand -base64 32", ex);
        }
    }

    /// <inheritdoc />
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            throw new ArgumentException("Plain text cannot be null or empty", nameof(plainText));
        }

        try
        {
            // Generate random nonce (12 bytes for AES-GCM)
            var nonce = new byte[12];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(nonce);
            }

            // Tag for authentication (16 bytes)
            var tag = new byte[16];

            // Convert plain text to bytes
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = new byte[plainBytes.Length];

            // Encrypt using AES-256-GCM
            using (var aes = new AesGcm(_masterKey, tag.Length))
            {
                aes.Encrypt(nonce, plainBytes, cipherBytes, tag);
            }

            // Combine: nonce:ciphertext:tag (all base64 encoded)
            var nonceBase64 = Convert.ToBase64String(nonce);
            var cipherBase64 = Convert.ToBase64String(cipherBytes);
            var tagBase64 = Convert.ToBase64String(tag);

            return $"{nonceBase64}:{cipherBase64}:{tagBase64}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encryption failed");
            throw new InvalidOperationException("Encryption failed. See inner exception for details.", ex);
        }
    }

    /// <inheritdoc />
    public string Decrypt(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
        {
            throw new ArgumentException("Encrypted text cannot be null or empty", nameof(encryptedText));
        }

        try
        {
            // Split the encrypted text into components
            var parts = encryptedText.Split(':');
            if (parts.Length != 3)
            {
                throw new FormatException(
                    "Invalid encrypted text format. Expected format: {nonce}:{ciphertext}:{tag}");
            }

            var nonce = Convert.FromBase64String(parts[0]);
            var cipherBytes = Convert.FromBase64String(parts[1]);
            var tag = Convert.FromBase64String(parts[2]);

            // Validate lengths
            if (nonce.Length != 12)
            {
                throw new FormatException($"Invalid nonce length: {nonce.Length} bytes. Expected 12 bytes.");
            }

            if (tag.Length != 16)
            {
                throw new FormatException($"Invalid tag length: {tag.Length} bytes. Expected 16 bytes.");
            }

            // Decrypt using AES-256-GCM
            var plainBytes = new byte[cipherBytes.Length];
            using (var aes = new AesGcm(_masterKey, tag.Length))
            {
                aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
            }

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "Decryption failed: Invalid format");
            throw;
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Decryption failed: Authentication failed or invalid key");
            throw new InvalidOperationException(
                "Decryption failed. The data may be corrupted or encrypted with a different key.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Decryption failed");
            throw new InvalidOperationException("Decryption failed. See inner exception for details.", ex);
        }
    }
}
