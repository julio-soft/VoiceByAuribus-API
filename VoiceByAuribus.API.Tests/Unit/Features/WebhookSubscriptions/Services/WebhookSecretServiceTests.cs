using VoiceByAuribus_API.Features.WebhookSubscriptions.Application.Services;
using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Tests.Unit.Features.WebhookSubscriptions.Services;

/// <summary>
/// Unit tests for WebhookSecretService.
/// Tests webhook secret generation, encryption, and HMAC-SHA256 signature computation.
/// Critical for webhook security and authentication.
/// </summary>
public class WebhookSecretServiceTests
{
    private readonly Mock<IEncryptionService> _mockEncryptionService;
    private readonly WebhookSecretService _service;

    public WebhookSecretServiceTests()
    {
        _mockEncryptionService = new Mock<IEncryptionService>();
        _service = new WebhookSecretService(_mockEncryptionService.Object);
    }

    #region GenerateSecret Tests

    [Fact]
    public void GenerateSecret_ReturnsValidHexString()
    {
        // Act
        var secret = _service.GenerateSecret();

        // Assert
        secret.Should().NotBeNullOrEmpty();
        secret.Should().HaveLength(64, "32 bytes = 64 hex characters");
        secret.Should().MatchRegex("^[0-9a-f]{64}$", "should be lowercase hex");
    }

    [Fact]
    public void GenerateSecret_CalledTwice_ReturnsDifferentValues()
    {
        // Act
        var secret1 = _service.GenerateSecret();
        var secret2 = _service.GenerateSecret();

        // Assert
        secret1.Should().NotBe(secret2, "should generate unique secrets");
    }

    [Fact]
    public void GenerateSecret_Always_MeetsMinimumLength()
    {
        // Act
        var secret = _service.GenerateSecret();

        // Assert
        _service.IsValidSecret(secret).Should().BeTrue();
    }

    #endregion

    #region IsValidSecret Tests

    [Theory]
    [InlineData("short", false)]
    [InlineData("123456789012345678901234567890", false)] // 30 chars
    [InlineData("12345678901234567890123456789012", true)] // 32 chars (minimum)
    [InlineData("123456789012345678901234567890123456789012345678901234567890123", true)] // 63 chars - valid
    [InlineData("1234567890123456789012345678901234567890123456789012345678901234", true)] // 64 chars - valid
    public void IsValidSecret_WithVariousLengths_ReturnsExpectedResult(string secret, bool expected)
    {
        // Act
        var result = _service.IsValidSecret(secret);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void IsValidSecret_WithInvalidInput_ReturnsFalse(string? secret, bool expected)
    {
        // Act
        var result = _service.IsValidSecret(secret!);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region EncryptSecret Tests

    [Fact]
    public void EncryptSecret_WithValidSecret_CallsEncryptionService()
    {
        // Arrange
        var plainSecret = "a".PadRight(32, 'a'); // 32 chars minimum
        var expectedEncrypted = "encrypted-value";
        _mockEncryptionService
            .Setup(x => x.Encrypt(plainSecret))
            .Returns(expectedEncrypted);

        // Act
        var result = _service.EncryptSecret(plainSecret);

        // Assert
        result.Should().Be(expectedEncrypted);
        _mockEncryptionService.Verify(x => x.Encrypt(plainSecret), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EncryptSecret_WithNullOrWhitespace_ThrowsArgumentException(string? secret)
    {
        // Act
        var act = () => _service.EncryptSecret(secret!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("plainTextSecret");
    }

    [Fact]
    public void EncryptSecret_WithTooShortSecret_ThrowsArgumentException()
    {
        // Arrange
        var shortSecret = "only-20-chars-here!"; // Less than 32

        // Act
        var act = () => _service.EncryptSecret(shortSecret);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*at least 32 characters*")
            .WithParameterName("plainTextSecret");
    }

    #endregion

    #region DecryptSecret Tests

    [Fact]
    public void DecryptSecret_WithValidEncryptedSecret_CallsEncryptionService()
    {
        // Arrange
        var encryptedSecret = "encrypted-secret-value";
        var expectedPlain = "a".PadRight(64, 'a'); // 64 char hex secret
        _mockEncryptionService
            .Setup(x => x.Decrypt(encryptedSecret))
            .Returns(expectedPlain);

        // Act
        var result = _service.DecryptSecret(encryptedSecret);

        // Assert
        result.Should().Be(expectedPlain);
        _mockEncryptionService.Verify(x => x.Decrypt(encryptedSecret), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DecryptSecret_WithNullOrWhitespace_ThrowsArgumentException(string? encryptedSecret)
    {
        // Act
        var act = () => _service.DecryptSecret(encryptedSecret!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("encryptedSecret");
    }

    #endregion

    #region ComputeHmacSignature Tests

    [Fact]
    public void ComputeHmacSignature_WithValidInputs_ReturnsValidHexString()
    {
        // Arrange
        var secret = "my-webhook-secret-key-32-chars";
        var payload = "{\"event\":\"test\",\"data\":{}}";

        // Act
        var signature = _service.ComputeHmacSignature(secret, payload);

        // Assert
        signature.Should().NotBeNullOrEmpty();
        signature.Should().HaveLength(64, "SHA256 = 32 bytes = 64 hex chars");
        signature.Should().MatchRegex("^[0-9a-f]{64}$", "should be lowercase hex");
    }

    [Fact]
    public void ComputeHmacSignature_WithSameInputs_ReturnsSameSignature()
    {
        // Arrange
        var secret = "consistent-secret-key-32-chars!!";
        var payload = "{\"event\":\"test\"}";

        // Act
        var signature1 = _service.ComputeHmacSignature(secret, payload);
        var signature2 = _service.ComputeHmacSignature(secret, payload);

        // Assert
        signature1.Should().Be(signature2, "HMAC should be deterministic");
    }

    [Fact]
    public void ComputeHmacSignature_WithDifferentSecrets_ReturnsDifferentSignatures()
    {
        // Arrange
        var secret1 = "first-secret-key-32-characters!!";
        var secret2 = "second-secret-key-32-characters!";
        var payload = "{\"event\":\"test\"}";

        // Act
        var signature1 = _service.ComputeHmacSignature(secret1, payload);
        var signature2 = _service.ComputeHmacSignature(secret2, payload);

        // Assert
        signature1.Should().NotBe(signature2, "different secrets should produce different signatures");
    }

    [Fact]
    public void ComputeHmacSignature_WithDifferentPayloads_ReturnsDifferentSignatures()
    {
        // Arrange
        var secret = "same-secret-key-32-characters!!!";
        var payload1 = "{\"event\":\"test1\"}";
        var payload2 = "{\"event\":\"test2\"}";

        // Act
        var signature1 = _service.ComputeHmacSignature(secret, payload1);
        var signature2 = _service.ComputeHmacSignature(secret, payload2);

        // Assert
        signature1.Should().NotBe(signature2, "different payloads should produce different signatures");
    }

    [Theory]
    [InlineData(null, "payload")]
    [InlineData("", "payload")]
    [InlineData("   ", "payload")]
    public void ComputeHmacSignature_WithInvalidSecret_ThrowsArgumentException(
        string? secret,
        string payload)
    {
        // Act
        var act = () => _service.ComputeHmacSignature(secret!, payload);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("plainTextSecret");
    }

    [Theory]
    [InlineData("valid-secret-key-32-characters!!", null)]
    [InlineData("valid-secret-key-32-characters!!", "")]
    [InlineData("valid-secret-key-32-characters!!", "   ")]
    public void ComputeHmacSignature_WithInvalidPayload_ThrowsArgumentException(
        string secret,
        string? payload)
    {
        // Act
        var act = () => _service.ComputeHmacSignature(secret, payload!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("payload");
    }

    [Fact]
    public void ComputeHmacSignature_WithRealWorldWebhookPayload_GeneratesValidSignature()
    {
        // Arrange
        var secret = _service.GenerateSecret(); // Use real generated secret
        var payload = @"{
            ""event"": ""conversion.completed"",
            ""conversion_id"": ""123e4567-e89b-12d3-a456-426614174000"",
            ""status"": ""completed"",
            ""created_at"": ""2025-11-25T12:00:00Z""
        }";

        // Act
        var signature = _service.ComputeHmacSignature(secret, payload);

        // Assert
        signature.Should().NotBeNullOrEmpty();
        signature.Should().MatchRegex("^[0-9a-f]{64}$");
        
        // Verify signature can be recomputed consistently
        var recomputedSignature = _service.ComputeHmacSignature(secret, payload);
        recomputedSignature.Should().Be(signature, "signature should be consistent");
    }

    [Fact]
    public void ComputeHmacSignature_WithKnownTestVector_MatchesExpectedOutput()
    {
        // Arrange - Test vector from HMAC-SHA256 specification
        var secret = "key";
        var payload = "The quick brown fox jumps over the lazy dog";
        // Expected HMAC-SHA256: f7bc83f430538424b13298e6aa6fb143ef4d59a14946175997479dbc2d1a3cd8
        var expectedSignature = "f7bc83f430538424b13298e6aa6fb143ef4d59a14946175997479dbc2d1a3cd8";

        // Act
        var signature = _service.ComputeHmacSignature(secret, payload);

        // Assert
        signature.Should().Be(expectedSignature, "should match known HMAC-SHA256 test vector");
    }

    #endregion

    #region Integration-Like Tests

    [Fact]
    public void FullWorkflow_GenerateEncryptDecryptSignature_WorksEndToEnd()
    {
        // This simulates the full webhook secret lifecycle
        
        // Step 1: Generate a new secret
        var plainSecret = _service.GenerateSecret();
        plainSecret.Should().HaveLength(64);
        
        // Step 2: Validate the generated secret
        _service.IsValidSecret(plainSecret).Should().BeTrue();
        
        // Step 3: Mock encryption (in real scenario, EncryptionService would handle this)
        var fakeEncrypted = "fake-encrypted:" + plainSecret;
        _mockEncryptionService
            .Setup(x => x.Encrypt(plainSecret))
            .Returns(fakeEncrypted);
        _mockEncryptionService
            .Setup(x => x.Decrypt(fakeEncrypted))
            .Returns(plainSecret);
        
        var encrypted = _service.EncryptSecret(plainSecret);
        encrypted.Should().Be(fakeEncrypted);
        
        // Step 4: Decrypt the secret
        var decrypted = _service.DecryptSecret(encrypted);
        decrypted.Should().Be(plainSecret);
        
        // Step 5: Use the decrypted secret to sign a payload
        var payload = "{\"event\":\"test\"}";
        var signature = _service.ComputeHmacSignature(decrypted, payload);
        signature.Should().NotBeNullOrEmpty();
        
        // Step 6: Verify signature consistency
        var verifySignature = _service.ComputeHmacSignature(plainSecret, payload);
        verifySignature.Should().Be(signature, "signatures should match");
    }

    #endregion
}
