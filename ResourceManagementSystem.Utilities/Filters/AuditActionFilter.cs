using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Security.Claims;

namespace ResourceManagementSystem.Utilities;

public sealed class AuditActionFilter : IAsyncActionFilter
{
    private readonly IAuditStore _auditStore;
    private readonly ILogger<AuditActionFilter> _logger;

    public AuditActionFilter(IAuditStore auditStore, ILogger<AuditActionFilter> logger)
    {
        _auditStore = auditStore;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var stopwatch = Stopwatch.StartNew();
        var correlationId = context.HttpContext.Items[CorrelationIdMiddleware.ItemKey]?.ToString()
            ?? context.HttpContext.TraceIdentifier;

        var executedContext = await next();

        stopwatch.Stop();

        var routeValues = context.ActionArguments
            .Where(kvp => kvp.Value is not null)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value?.ToString() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

        var entry = new AuditEntry
        {
            StaffId = TryParseInt(context.HttpContext.User.FindFirst("StaffId")?.Value),
            Role = context.HttpContext.User.FindFirst(ClaimTypes.Role)?.Value ?? context.HttpContext.User.FindFirst("BranchRole")?.Value,
            BranchCode = context.HttpContext.User.FindFirst("BranchCode")?.Value,
            Controller = context.RouteData.Values["controller"]?.ToString(),
            Action = context.RouteData.Values["action"]?.ToString(),
            HttpMethod = context.HttpContext.Request.Method,
            Path = context.HttpContext.Request.Path.Value,
            CorrelationId = correlationId,
            StatusCode = executedContext.Exception is null ? context.HttpContext.Response.StatusCode : StatusCodes.Status500InternalServerError,
            DurationMs = stopwatch.ElapsedMilliseconds,
            RouteValues = routeValues,
            LoggedAtUtc = DateTime.UtcNow
        };

        await _auditStore.AppendAsync(entry);

        if (entry.DurationMs > 2000)
        {
            _logger.LogWarning(
                "Slow action audited. Controller: {Controller}, Action: {Action}, DurationMs: {DurationMs}, CorrelationId: {CorrelationId}",
                entry.Controller,
                entry.Action,
                entry.DurationMs,
                entry.CorrelationId);
        }
    }

    private static int? TryParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value, out var result) ? result : null;
    }
}
