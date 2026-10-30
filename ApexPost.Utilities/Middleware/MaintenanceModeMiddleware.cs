using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ApexPost.Utilities.Options;

namespace ApexPost.Utilities.Middleware;

/// <summary>
/// Returns 503 with Retry-After while <c>MaintenanceMode:Enabled</c> is true. <c>/health</c> stays up so
/// the load balancer can tell "deliberately paused" from "crashed". Uses IOptionsMonitor because the
/// middleware is a singleton and the flag must be re-read on every request (reload-on-change).
/// </summary>
public sealed partial class MaintenanceModeMiddleware(
    RequestDelegate next,
    IOptionsMonitor<MaintenanceModeOptions> options,
    ILogger<MaintenanceModeMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var current = options.CurrentValue;
        if (!current.Enabled || current.AlwaysAvailablePaths.Any(p => context.Request.Path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        LogRejected(context.Request.Method, context.Request.Path.Value ?? "/");
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers.RetryAfter = Math.Max(1, current.RetryAfterSeconds).ToString(CultureInfo.InvariantCulture);
        await ProblemResponses.WriteAsync(context, new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = "Service unavailable",
            Detail = current.Message,
        }).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Maintenance mode rejected {Method} {Path}")]
    private partial void LogRejected(string method, string path);
}
