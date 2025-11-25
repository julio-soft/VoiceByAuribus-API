using FluentValidation;
using VoiceByAuribus_API.Features.WebhookSubscriptions.Application.Dtos;

namespace VoiceByAuribus_API.Features.WebhookSubscriptions.Application.Validators;

/// <summary>
/// Validator for CreateWebhookSubscriptionDto with security checks.
/// </summary>
public class CreateWebhookSubscriptionDtoValidator : AbstractValidator<CreateWebhookSubscriptionDto>
{
    public CreateWebhookSubscriptionDtoValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty()
            .WithMessage("URL is required")
            .Must(BeValidHttpsUrl)
            .WithMessage("URL must be a valid HTTPS URL (HTTP is not allowed)")
            .Must(NotBePrivateIpOrLocalhost)
            .WithMessage("URL cannot point to localhost or private IP addresses (SSRF protection)");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .WithMessage("Description cannot exceed 500 characters");

        RuleFor(x => x.Events)
            .NotEmpty()
            .WithMessage("At least one event must be subscribed");
    }

    private static bool BeValidHttpsUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps;
    }

    private static bool NotBePrivateIpOrLocalhost(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        // Check for localhost
        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            uri.Host == "127.0.0.1" ||
            uri.Host == "::1")
        {
            return false;
        }

        // Check for private IP ranges (basic SSRF protection)
        // A more comprehensive check would parse the IP and verify ranges
        if (uri.Host.StartsWith("10.") ||
            uri.Host.StartsWith("192.168.") ||
            uri.Host.StartsWith("172.16.") ||
            uri.Host.StartsWith("172.17.") ||
            uri.Host.StartsWith("172.18.") ||
            uri.Host.StartsWith("172.19.") ||
            uri.Host.StartsWith("172.20.") ||
            uri.Host.StartsWith("172.21.") ||
            uri.Host.StartsWith("172.22.") ||
            uri.Host.StartsWith("172.23.") ||
            uri.Host.StartsWith("172.24.") ||
            uri.Host.StartsWith("172.25.") ||
            uri.Host.StartsWith("172.26.") ||
            uri.Host.StartsWith("172.27.") ||
            uri.Host.StartsWith("172.28.") ||
            uri.Host.StartsWith("172.29.") ||
            uri.Host.StartsWith("172.30.") ||
            uri.Host.StartsWith("172.31.") ||
            uri.Host.StartsWith("169.254.")) // Link-local
        {
            return false;
        }

        return true;
    }
}
