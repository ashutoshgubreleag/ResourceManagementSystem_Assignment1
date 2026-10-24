
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ResourceManagementSystem.Utilities;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(
        RequestDelegate next,
        ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);

        context.Items[ItemKey] = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (_logger.BeginScope(
            new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId
            }))
        {
            using (LogContext.BeginScope(correlationId))
            {
                _logger.LogInformation(
                    "Handling HTTP {Method} {Path}",
                    context.Request.Method,
                    context.Request.Path);

                await _next(context);
            }
        }
    }

    private static string GetOrCreateCorrelationId(
        HttpContext context)
    {
        var incomingId = context.Request.Headers[HeaderName]
            .FirstOrDefault();

        // Accept only a single, bounded, safe identifier.
        if (!string.IsNullOrWhiteSpace(incomingId)
            && incomingId.Length <= 128
            && incomingId.All(c =>
                char.IsAsciiLetterOrDigit(c)
                || c is '-' or '_' or '.'))
        {
            return incomingId;
        }

        return ActivityTraceId.CreateRandom().ToString();
    }
}

// Provides a small helper to avoid needing another logging package.
internal static class LogContext
{
    public static IDisposable? BeginScope(string correlationId)
        => CorrelationScope.Begin(correlationId);
}

internal static class CorrelationScope
{
    private static readonly AsyncLocal<string?> CurrentId = new();

    public static IDisposable Begin(string correlationId)
    {
        var previous = CurrentId.Value;
        CurrentId.Value = correlationId;
        return new Scope(() => CurrentId.Value = previous);
    }

    private sealed class Scope(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
