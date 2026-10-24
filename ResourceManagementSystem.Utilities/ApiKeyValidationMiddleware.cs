using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace ResourceManagementSystem.Utilities;

public sealed class ApiKeyValidationMiddleware
{
    private const string HeaderName = "x-api-key";
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiKeyValidationMiddleware> _logger;

    public ApiKeyValidationMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<ApiKeyValidationMiddleware> logger)
    {
        _next = next;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();

        if (endpoint?.Metadata.GetMetadata<ApiKeyRequiredAttribute>() is null)
        {
            await _next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var configuredKey = _configuration["Integration:ApiKey"]
            ?? _configuration["ApiKeys:IntegrationKey"]
            ?? _configuration["Authentication:ApiKey"];

        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            _logger.LogError("API key is not configured for protected integration endpoint {Path}", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Type = "https://httpstatuses.com/500",
                Title = "Server configuration error",
                Status = StatusCodes.Status500InternalServerError,
                Detail = "The integration API key is not configured."
            });
            return;
        }

        var requestKey = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(requestKey))
        {
            _logger.LogWarning("Rejected integration request without API key. Path: {Path}; CorrelationId: {CorrelationId}", context.Request.Path, context.Items[CorrelationIdMiddleware.ItemKey]);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Type = "https://httpstatuses.com/401",
                Title = "Unauthorized",
                Status = StatusCodes.Status401Unauthorized,
                Detail = "A valid API key is required."
            });
            return;
        }

        var configuredBytes = Encoding.UTF8.GetBytes(configuredKey);
        var requestBytes = Encoding.UTF8.GetBytes(requestKey);

        if (configuredBytes.Length != requestBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(configuredBytes, requestBytes))
        {
            _logger.LogWarning("Rejected integration request with invalid API key. Path: {Path}; CorrelationId: {CorrelationId}", context.Request.Path, context.Items[CorrelationIdMiddleware.ItemKey]);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Type = "https://httpstatuses.com/401",
                Title = "Unauthorized",
                Status = StatusCodes.Status401Unauthorized,
                Detail = "The supplied API key is invalid."
            });
            return;
        }

        await _next(context);
    }
}
