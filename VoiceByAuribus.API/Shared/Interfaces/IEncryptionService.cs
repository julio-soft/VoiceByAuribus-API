namespace VoiceByAuribus_API.Shared.Interfaces;

/// <summary>
/// Service for encrypting and decrypting sensitive data using AES-256-GCM.
/// Uses a master key from AWS Secrets Manager for encryption operations.
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts a plain text string using AES-256-GCM.
    /// Returns base64-encoded ciphertext in format: {iv}:{ciphertext}:{tag}
    /// </summary>
    /// <param name="plainText">The plain text to encrypt.</param>
    /// <returns>Base64-encoded encrypted string.</returns>
    string Encrypt(string plainText);

    /// <summary>
    /// Decrypts an encrypted string that was encrypted using AES-256-GCM.
    /// Expects format: {iv}:{ciphertext}:{tag}
    /// </summary>
    /// <param name="encryptedText">The encrypted text to decrypt.</param>
    /// <returns>The decrypted plain text.</returns>
    string Decrypt(string encryptedText);
}
