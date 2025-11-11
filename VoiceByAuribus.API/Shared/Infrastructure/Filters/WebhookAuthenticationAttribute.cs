using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;

namespace VoiceByAuribus_API.Shared.Infrastructure.Filters;

/// <summary>
/// Authorization filter for webhook endpoints that validates API key.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class WebhookAuthenticationAttribute : Attribute, IAuthorizationFilter
{
    private const string ApiKeyHeaderName = "X-Webhook-Api-Key";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var configuration = context.HttpContext.RequestServices.GetService(typeof(IConfiguration)) as IConfiguration;

        if (configuration is null)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            return;
        }

        var expectedApiKey = configuration["Webhooks:ApiKey"];

        if (string.IsNullOrWhiteSpace(expectedApiKey))
        {
            context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyValues))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var providedApiKey = apiKeyValues.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(providedApiKey) || providedApiKey != expectedApiKey)
        {
            context.Result = new UnauthorizedResult();
            return;
        }
    }
}
