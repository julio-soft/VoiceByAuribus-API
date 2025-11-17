using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using VoiceByAuribus_API.Shared.Domain;

namespace VoiceByAuribus_API.Shared.Infrastructure.Middleware;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // Log with structured context for CloudWatch
            _logger.LogError(ex, 
                "Unhandled exception: {ExceptionType} | Path: {RequestPath} | Method: {RequestMethod} | TraceId: {TraceId}",
                ex.GetType().Name,
                context.Request.Path,
                context.Request.Method,
                context.TraceIdentifier);
            
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = exception switch
        {
            UnauthorizedAccessException => CreateResponse(
                HttpStatusCode.Unauthorized,
                "Unauthorized",
                exception.Message),

            ArgumentException or ArgumentNullException => CreateResponse(
                HttpStatusCode.BadRequest,
                "Bad Request",
                exception.Message),

            KeyNotFoundException => CreateResponse(
                HttpStatusCode.NotFound,
                "Not Found",
                exception.Message),

            _ => CreateResponse(
                HttpStatusCode.InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred. Please try again later.")
        };

        context.Response.StatusCode = (int)response.StatusCode;

        var jsonResponse = JsonSerializer.Serialize(response.Body, _jsonOptions);
        await context.Response.WriteAsync(jsonResponse);
    }

    private static (HttpStatusCode StatusCode, ApiResponse<object> Body) CreateResponse(
        HttpStatusCode statusCode,
        string title,
        string message)
    {
        var response = ApiResponse<object>.ErrorResponse(message, new[] { title }.ToList());
        return (statusCode, response);
    }
}
