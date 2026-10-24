
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;

namespace ResourceManagementSystem.Utilities;

public sealed class MaintenanceModeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MaintenanceModeMiddleware> _logger;

    public MaintenanceModeMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<MaintenanceModeMiddleware> logger)
    {
        _next = next;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var enabled = _configuration.GetValue<bool>(
            "MaintenanceMode:Enabled");

        // Always allow health checks during maintenance.
        if (context.Request.Path.Equals(
                "/health",
                StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (!enabled)
        {
            await _next(context);
            return;
        }

        var retryAfterSeconds = Math.Max(
            1,
            _configuration.GetValue<int>(
                "MaintenanceMode:RetryAfterSeconds",
                3600));

        var message = _configuration[
            "MaintenanceMode:Message"]
            ?? "The application is temporarily unavailable.";

        var correlationId =
            context.Items[CorrelationIdMiddleware.ItemKey]?.ToString()
            ?? context.TraceIdentifier;

        _logger.LogWarning(
            "Maintenance mode rejected request {Method} {Path}. " +
            "CorrelationId: {CorrelationId}",
            context.Request.Method,
            context.Request.Path,
            correlationId);

        context.Response.StatusCode =
            StatusCodes.Status503ServiceUnavailable;

        context.Response.Headers["Retry-After"] =
            retryAfterSeconds.ToString(
                System.Globalization.CultureInfo.InvariantCulture);

        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Type = "https://httpstatuses.com/503",
            Title = "Service Unavailable",
            Status = StatusCodes.Status503ServiceUnavailable,
            Detail = message
        };

        problem.Extensions["correlationId"] = correlationId;

        await context.Response.WriteAsJsonAsync(problem);
    }
}
