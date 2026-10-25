using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ResourceManagementSystem.Utilities.Middleware;
using ResourceManagementSystem.Utilities.Options;

namespace ResourceManagementSystem.Utilities.Filters;

/// <summary>
/// Times each action and logs a warning above <c>Performance:SlowActionThresholdMs</c>. It also adds a
/// <c>Server-Timing</c> header so the browser dev tools show action time separately from network time.
/// A filter rather than middleware because it reports per <i>action</i> (controller + action name) and
/// excludes static files, authentication and other pipeline work that request-timing middleware
/// already covers; the two together show whether slowness is in our code or around it.
/// </summary>
public sealed partial class PerformanceFilter(IOptionsMonitor<PerformanceOptions> options, ILogger<PerformanceFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var started = Stopwatch.GetTimestamp();
        await next().ConfigureAwait(false);
        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        if (!context.HttpContext.Response.HasStarted)
        {
            context.HttpContext.Response.Headers["Server-Timing"] = string.Create(CultureInfo.InvariantCulture, $"action;dur={elapsed:0.0}");
        }

        var threshold = options.CurrentValue.SlowActionThresholdMs;
        if (elapsed > threshold)
        {
            LogSlowAction(
                context.RouteData.Values["controller"]?.ToString() ?? "?",
                context.RouteData.Values["action"]?.ToString() ?? "?",
                Math.Round(elapsed, 1),
                threshold,
                CorrelationIdMiddleware.Get(context.HttpContext));
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Slow action {Controller}.{Action} took {ElapsedMs} ms (threshold {ThresholdMs} ms, CorrelationId {CorrelationId})")]
    private partial void LogSlowAction(string controller, string action, double elapsedMs, int thresholdMs, string correlationId);
}
