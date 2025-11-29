using FluentAssertions;
using FluentValidation.TestHelper;
using VoiceByAuribus_API.Features.WebhookSubscriptions.Application.Dtos;
using VoiceByAuribus_API.Features.WebhookSubscriptions.Application.Validators;
using VoiceByAuribus_API.Features.WebhookSubscriptions.Domain;

namespace VoiceByAuribus_API.Tests.Unit.Features.WebhookSubscriptions.Validators;

/// <summary>
/// Unit tests for CreateWebhookSubscriptionDtoValidator.
/// Tests HTTPS requirement and SSRF protection.
/// </summary>
public class CreateWebhookSubscriptionDtoValidatorTests
{
    private readonly CreateWebhookSubscriptionDtoValidator _validator = new();

    #region URL Validation Tests

    /// <summary>
    /// Tests that valid HTTPS URLs pass validation.
    /// </summary>
    [Theory]
    [InlineData("https://example.com/webhook")]
    [InlineData("https://api.example.com/webhooks/receive")]
    [InlineData("https://webhook.example.com:8443/events")]
    [InlineData("https://sub.domain.example.com/path/to/webhook")]
    public async Task Validate_WithValidHttpsUrl_Passes(string url)
    {
        // Arrange
        var dto = new CreateWebhookSubscriptionDto
        {
            Url = url,
            Events = [WebhookEvent.ConversionCompleted]
        };

        // Act
        var result = await _validator.TestValidateAsync(dto);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Url);
    }

    /// <summary>
    /// Tests that HTTP (non-HTTPS) URLs fail validation.
    /// </summary>
    [Theory]
    [InlineData("http://example.com/webhook")]
    [InlineData("http://api.example.com/webhooks")]
    public async Task Validate_WithHttpUrl_Fails(string url)
    {
        // Arrange
        var dto = new CreateWebhookSubscriptionDto
        {
            Url = url,
            Events = [WebhookEvent.ConversionCompleted]
        };

        // Act
        var result = await _validator.TestValidateAsync(dto);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Url)
            .WithErrorMessage("URL must be a valid HTTPS URL (HTTP is not allowed)");
    }

    /// <summary>
    /// Tests that empty or null URL fails validation.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_WithEmptyUrl_Fails(string? url)
    {
        // Arrange
        var dto = new CreateWebhookSubscriptionDto
        {
            Url = url!,
            Events = [WebhookEvent.ConversionCompleted]
        };

        // Act
        var result = await _validator.TestValidateAsync(dto);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Url)
            .WithErrorMessage("URL is required");
    }

    /// <summary>
    /// Tests that invalid URL format fails validation.
    /// </summary>
    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com/webhook")]
    [InlineData("javascript:alert('xss')")]
    public async Task Validate_WithInvalidUrlFormat_Fails(string url)
    {
        // Arrange
        var dto = new CreateWebhookSubscriptionDto
        {
            Url = url,
            Events = [WebhookEvent.ConversionCompleted]
        };

        // Act
        var result = await _validator.TestValidateAsync(dto);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Url);
    }

    #endregion

    #region SSRF Protection Tests

    /// <summary>
    /// Tests that localhost URLs are rejected (SSRF protection).
    /// </summary>
    [Theory]
    [InlineData("https://localhost/webhook")]
    [InlineData("https://localhost:8080/webhook")]
    [InlineData("https://127.0.0.1/webhook")]
    [InlineData("https://127.0.0.1:8443/webhook")]
    public async Task Validate_WithLocalhostUrl_FailsWithSsrfProtection(string url)
    {
        // Arrange
        var dto = new CreateWebhookSubscriptionDto
        {
            Url = url,
            Events = [WebhookEvent.ConversionCompleted]
        };

        // Act
        var result = await _validator.TestValidateAsync(dto);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Url)
            .WithErrorMessage("URL cannot point to localhost or private IP addresses (SSRF protection)");
    }

    /// <summary>
    /// Tests that private IP ranges are rejected (SSRF protection).
    /// </summary>
    [Theory]
    [InlineData("https://10.0.0.1/webhook")] // 10.0.0.0/8
    [InlineData("https://10.255.255.255/webhook")]
    [InlineData("https://192.168.1.1/webhook")] // 192.168.0.0/16
    [InlineData("https://192.168.255.255/webhook")]
    [InlineData("https://172.16.0.1/webhook")] // 172.16.0.0/12
    [InlineData("https://172.31.255.255/webhook")]
    [InlineData("https://169.254.1.1/webhook")] // Link-local
    public async Task Validate_WithPrivateIpUrl_FailsWithSsrfProtection(string url)
    {
        // Arrange
        var dto = new CreateWebhookSubscriptionDto
        {
            Url = url,
            Events = [WebhookEvent.ConversionCompleted]
        };

        // Act
        var result = await _validator.TestValidateAsync(dto);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Url)
            .WithErrorMessage("URL cannot point to localhost or private IP addresses (SSRF protection)");
    }

    /// <summary>
    /// Tests that public IPs pass validation.
    /// </summary>
    [Theory]
    [InlineData("https://1.1.1.1/webhook")] // Cloudflare DNS
    [InlineData("https://8.8.8.8/webhook")] // Google DNS
    [InlineData("https://142.250.185.46/webhook")] // Public IP
    public async Task Validate_WithPublicIpUrl_Passes(string url)
    {
        // Arrange
        var dto = new CreateWebhookSubscriptionDto
        {
            Url = url,
            Events = [WebhookEvent.ConversionCompleted]
        };

        // Act
        var result = await _validator.TestValidateAsync(dto);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Url);
    }

    #endregion

    #region Description Validation Tests

    /// <summary>
    /// Tests that description within 500 characters passes validation.
    /// </summary>
    [Fact]
    public async Task Validate_WithValidDescription_Passes()
    {
        // Arrange
        var dto = new CreateWebhookSubscriptionDto
        {
            Url = "https://example.com/webhook",
            Description = "This is a valid description for the webhook subscription",
            Events = [WebhookEvent.ConversionCompleted]
        };

        // Act
        var result = await _validator.TestValidateAsync(dto);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    /// <summary>
    /// Tests that description exceeding 500 characters fails validation.
    /// </summary>
    [Fact]
    public async Task Validate_WithDescriptionOver500Chars_Fails()
    {
        // Arrange
        var dto = new CreateWebhookSubscriptionDto
        {
            Url = "https://example.com/webhook",
            Description = new string('x', 501),
            Events = [WebhookEvent.ConversionCompleted]
        };

        // Act
        var result = await _validator.TestValidateAsync(dto);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Description)
            .WithErrorMessage("Description cannot exceed 500 characters");
    }

    /// <summary>
    /// Tests that null description passes validation (optional field).
    /// </summary>
    [Fact]
    public async Task Validate_WithNullDescription_Passes()
    {
        // Arrange
        var dto = new CreateWebhookSubscriptionDto
        {
            Url = "https://example.com/webhook",
            Description = null,
            Events = [WebhookEvent.ConversionCompleted]
        };

        // Act
        var result = await _validator.TestValidateAsync(dto);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    #endregion

    #region Events Validation Tests

    /// <summary>
    /// Tests that at least one event must be subscribed.
    /// </summary>
    [Fact]
    public async Task Validate_WithNoEvents_Fails()
    {
        // Arrange
        var dto = new CreateWebhookSubscriptionDto
        {
            Url = "https://example.com/webhook",
            Events = []
        };

        // Act
        var result = await _validator.TestValidateAsync(dto);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Events)
            .WithErrorMessage("At least one event must be subscribed");
    }

    /// <summary>
    /// Tests that null events list fails validation.
    /// </summary>
    [Fact]
    public async Task Validate_WithNullEvents_Fails()
    {
        // Arrange
        var dto = new CreateWebhookSubscriptionDto
        {
            Url = "https://example.com/webhook",
            Events = null!
        };

        // Act
        var result = await _validator.TestValidateAsync(dto);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Events);
    }

    /// <summary>
    /// Tests that single event passes validation.
    /// </summary>
    [Fact]
    public async Task Validate_WithSingleEvent_Passes()
    {
        // Arrange
        var dto = new CreateWebhookSubscriptionDto
        {
            Url = "https://example.com/webhook",
            Events = [WebhookEvent.ConversionCompleted]
        };

        // Act
        var result = await _validator.TestValidateAsync(dto);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Events);
    }

    /// <summary>
    /// Tests that multiple events pass validation.
    /// </summary>
    [Fact]
    public async Task Validate_WithMultipleEvents_Passes()
    {
        // Arrange
        var dto = new CreateWebhookSubscriptionDto
        {
            Url = "https://example.com/webhook",
            Events = new WebhookEvent[]
            {
                WebhookEvent.ConversionCompleted,
                WebhookEvent.ConversionFailed
            }
        };

        // Act
        var result = await _validator.TestValidateAsync(dto);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Events);
    }

    #endregion

    #region Complete DTO Validation Tests

    /// <summary>
    /// Tests that a complete valid DTO passes all validations.
    /// </summary>
    [Fact]
    public async Task Validate_WithCompleteValidDto_PassesAllValidations()
    {
        // Arrange
        var dto = new CreateWebhookSubscriptionDto
        {
            Url = "https://api.example.com/webhooks/receive",
            Description = "Production webhook for voice conversion notifications",
            Events = new WebhookEvent[]
            {
                WebhookEvent.ConversionCompleted,
                WebhookEvent.ConversionFailed
            }
        };

        // Act
        var result = await _validator.TestValidateAsync(dto);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    #endregion
}
