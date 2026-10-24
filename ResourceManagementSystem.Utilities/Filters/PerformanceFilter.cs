using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ResourceManagementSystem.Utilities;

public sealed class PerformanceFilter : IAsyncActionFilter
{
    private readonly ILogger<PerformanceFilter> _logger;
    private readonly IConfiguration _configuration;

    public PerformanceFilter(ILogger<PerformanceFilter> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var stopwatch = Stopwatch.StartNew();
        var thresholdMs = _configuration.GetValue<int>("Performance:ActionThresholdMs", 2000);

        var executedContext = await next();

        stopwatch.Stop();

        if (stopwatch.ElapsedMilliseconds > thresholdMs)
        {
            _logger.LogWarning(
                "Slow action detected. Controller: {Controller}, Action: {Action}, DurationMs: {DurationMs}",
                context.RouteData.Values["controller"],
                context.RouteData.Values["action"],
                stopwatch.ElapsedMilliseconds);
        }

        if (executedContext.Exception is not null)
        {
            _logger.LogError(
                executedContext.Exception,
                "Action failed. Controller: {Controller}, Action: {Action}",
                context.RouteData.Values["controller"],
                context.RouteData.Values["action"]);
        }
    }
}
