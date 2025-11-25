using FluentValidation;
using VoiceByAuribus_API.Features.WebhookSubscriptions.Application.BackgroundServices;
using VoiceByAuribus_API.Features.WebhookSubscriptions.Application.Dtos;
using VoiceByAuribus_API.Features.WebhookSubscriptions.Application.Services;
using VoiceByAuribus_API.Features.WebhookSubscriptions.Application.Validators;

namespace VoiceByAuribus_API.Features.WebhookSubscriptions;

/// <summary>
/// Dependency injection registration for the WebhookSubscriptions feature.
/// Registers all services, validators, and background processors required for webhook functionality.
/// </summary>
public static class WebhookSubscriptionsModule
{
    /// <summary>
    /// Adds webhook subscription feature services to the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddWebhookSubscriptionsFeature(this IServiceCollection services)
    {
        // Core webhook services
        services.AddScoped<IWebhookSecretService, WebhookSecretService>();
        services.AddScoped<IWebhookDeliveryService, WebhookDeliveryService>();
        services.AddScoped<IWebhookSubscriptionService, WebhookSubscriptionService>();
        services.AddScoped<IWebhookEventPublisher, WebhookEventPublisher>();

        // Background processor for webhook delivery
        // Registered as both singleton service and hosted service
        services.AddSingleton<WebhookDeliveryProcessorService>();
        services.AddHostedService<WebhookDeliveryProcessorService>(provider =>
            provider.GetRequiredService<WebhookDeliveryProcessorService>());

        // FluentValidation validators
        services.AddScoped<IValidator<CreateWebhookSubscriptionDto>, CreateWebhookSubscriptionDtoValidator>();

        // Configure HttpClient for webhook delivery
        services.AddHttpClient("WebhookClient", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "VoiceByAuribus-Webhook/1.0");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            // Don't follow redirects - webhook endpoints should respond directly
            AllowAutoRedirect = false,
            // Enable TLS 1.2 and 1.3 only for security
            SslProtocols = System.Security.Authentication.SslProtocols.Tls12 |
                          System.Security.Authentication.SslProtocols.Tls13
        });

        return services;
    }
}
