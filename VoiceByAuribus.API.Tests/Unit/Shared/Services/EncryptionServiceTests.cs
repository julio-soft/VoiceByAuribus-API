using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using VoiceByAuribus_API.Shared.Infrastructure.Services;

namespace VoiceByAuribus_API.Tests.Unit.Shared.Services;

/// <summary>
/// Unit tests for EncryptionService (AES-256-GCM encryption/decryption).
/// Critical for webhook secret encryption and sensitive data protection.
/// </summary>
public class EncryptionServiceTests
{
    private readonly ILogger<EncryptionService> _logger;

    public EncryptionServiceTests()
    {
        _logger = new Mock<ILogger<EncryptionService>>().Object;
    }

    [Fact]
    public void Constructor_WithValidMasterKey_InitializesSuccessfully()
    {
        // Arrange
        var configuration = CreateConfiguration(GenerateValidMasterKey());

        // Act
        var service = new EncryptionService(configuration, _logger);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithMissingMasterKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var act = () => new EncryptionService(configuration, _logger);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Encryption master key not configured*");
    }

    [Fact]
    public void Constructor_WithInvalidBase64MasterKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = CreateConfiguration("not-valid-base64!!!");

        // Act
        var act = () => new EncryptionService(configuration, _logger);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Invalid master key format*");
    }

    [Fact]
    public void Constructor_WithWrongKeyLength_ThrowsInvalidOperationException()
    {
        // Arrange - 16 bytes instead of 32
        var shortKey = Convert.ToBase64String(new byte[16]);
        var configuration = CreateConfiguration(shortKey);

        // Act
        var act = () => new EncryptionService(configuration, _logger);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Invalid master key length*");
    }

    [Fact]
    public void Encrypt_WithValidPlainText_ReturnsEncryptedString()
    {
        // Arrange
        var service = CreateService();
        var plainText = "test-secret-value";

        // Act
        var encrypted = service.Encrypt(plainText);

        // Assert
        encrypted.Should().NotBeNullOrEmpty();
        encrypted.Should().Contain(":"); // Format: nonce:ciphertext:tag
        encrypted.Split(':').Should().HaveCount(3);
    }

    [Fact]
    public void Encrypt_WithNullText_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act
        var act = () => service.Encrypt(null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("plainText");
    }

    [Fact]
    public void Encrypt_WithEmptyText_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act
        var act = () => service.Encrypt(string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("plainText");
    }

    [Fact]
    public void Encrypt_CalledTwiceWithSameInput_ReturnsDifferentResults()
    {
        // Arrange
        var service = CreateService();
        var plainText = "same-input-text";

        // Act
        var encrypted1 = service.Encrypt(plainText);
        var encrypted2 = service.Encrypt(plainText);

        // Assert - Different nonces should produce different encrypted outputs
        encrypted1.Should().NotBe(encrypted2);
    }

    [Fact]
    public void Decrypt_WithValidEncryptedText_ReturnsOriginalPlainText()
    {
        // Arrange
        var service = CreateService();
        var plainText = "original-secret-value";
        var encrypted = service.Encrypt(plainText);

        // Act
        var decrypted = service.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(plainText);
    }

    [Fact]
    public void Decrypt_WithNullText_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act
        var act = () => service.Decrypt(null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("encryptedText");
    }

    [Fact]
    public void Decrypt_WithEmptyText_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act
        var act = () => service.Decrypt(string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("encryptedText");
    }

    [Fact]
    public void Decrypt_WithInvalidFormat_ThrowsFormatException()
    {
        // Arrange
        var service = CreateService();
        var invalidFormat = "only-one-part"; // Should be nonce:ciphertext:tag

        // Act
        var act = () => service.Decrypt(invalidFormat);

        // Assert
        act.Should().Throw<FormatException>()
            .WithMessage("*Invalid encrypted text format*");
    }

    [Fact]
    public void Decrypt_WithInvalidNonceLength_ThrowsFormatException()
    {
        // Arrange
        var service = CreateService();
        // Create invalid format with wrong nonce length (8 bytes instead of 12)
        var invalidNonce = Convert.ToBase64String(new byte[8]);
        var validCipher = Convert.ToBase64String(new byte[16]);
        var validTag = Convert.ToBase64String(new byte[16]);
        var invalidEncrypted = $"{invalidNonce}:{validCipher}:{validTag}";

        // Act
        var act = () => service.Decrypt(invalidEncrypted);

        // Assert
        act.Should().Throw<FormatException>()
            .WithMessage("*Invalid nonce length*");
    }

    [Fact]
    public void Decrypt_WithInvalidTagLength_ThrowsFormatException()
    {
        // Arrange
        var service = CreateService();
        // Create invalid format with wrong tag length (8 bytes instead of 16)
        var validNonce = Convert.ToBase64String(new byte[12]);
        var validCipher = Convert.ToBase64String(new byte[16]);
        var invalidTag = Convert.ToBase64String(new byte[8]);
        var invalidEncrypted = $"{validNonce}:{validCipher}:{invalidTag}";

        // Act
        var act = () => service.Decrypt(invalidEncrypted);

        // Assert
        act.Should().Throw<FormatException>()
            .WithMessage("*Invalid tag length*");
    }

    [Fact]
    public void Decrypt_WithCorruptedCiphertext_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = CreateService();
        var plainText = "original-text";
        var encrypted = service.Encrypt(plainText);
        
        // Corrupt the ciphertext part
        var parts = encrypted.Split(':');
        parts[1] = Convert.ToBase64String(new byte[16]); // Replace with random bytes
        var corruptedEncrypted = string.Join(':', parts);

        // Act
        var act = () => service.Decrypt(corruptedEncrypted);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Decryption failed*");
    }

    [Fact]
    public void Decrypt_WithWrongMasterKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var service1 = CreateService();
        var plainText = "secret-data";
        var encrypted = service1.Encrypt(plainText);

        // Create second service with different master key
        var service2 = CreateService(GenerateValidMasterKey());

        // Act
        var act = () => service2.Decrypt(encrypted);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Decryption failed*");
    }

    [Theory]
    [InlineData("short")]
    [InlineData("medium-length-secret-value")]
    [InlineData("very-long-secret-value-with-many-characters-to-test-larger-plaintexts-encryption-and-decryption")]
    [InlineData("special-chars-!@#$%^&*()_+-=[]{}|;:',.<>?")]
    [InlineData("unicode-émojis-🔒🔑✅")]
    public void EncryptDecrypt_WithVariousInputs_MaintainsDataIntegrity(string plainText)
    {
        // Arrange
        var service = CreateService();

        // Act
        var encrypted = service.Encrypt(plainText);
        var decrypted = service.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(plainText);
    }

    [Fact]
    public void EncryptedFormat_HasCorrectStructure()
    {
        // Arrange
        var service = CreateService();
        var plainText = "test-value";

        // Act
        var encrypted = service.Encrypt(plainText);
        var parts = encrypted.Split(':');

        // Assert
        parts.Should().HaveCount(3, "format should be nonce:ciphertext:tag");
        
        // Verify each part is valid base64
        var nonce = Convert.FromBase64String(parts[0]);
        var ciphertext = Convert.FromBase64String(parts[1]);
        var tag = Convert.FromBase64String(parts[2]);

        nonce.Length.Should().Be(12, "AES-GCM nonce should be 12 bytes");
        tag.Length.Should().Be(16, "AES-GCM tag should be 16 bytes");
        ciphertext.Length.Should().BeGreaterThan(0, "ciphertext should not be empty");
    }

    // Helper methods

    private EncryptionService CreateService(string? masterKey = null)
    {
        var key = masterKey ?? GenerateValidMasterKey();
        var configuration = CreateConfiguration(key);
        return new EncryptionService(configuration, _logger);
    }

    private static IConfiguration CreateConfiguration(string masterKey)
    {
        var configData = new Dictionary<string, string?>
        {
            ["Encryption:MasterKey"] = masterKey
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    private static string GenerateValidMasterKey()
    {
        var key = new byte[32]; // AES-256 requires 32 bytes
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(key);
        return Convert.ToBase64String(key);
    }
}
