
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ResourceManagementSystem.Utilities;

public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        var correlationId =
            context.Items[CorrelationIdMiddleware.ItemKey]?.ToString()
            ?? context.TraceIdentifier;

        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "/";

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            var statusCode = context.Response.StatusCode;
            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

            using (_logger.BeginScope(
                new Dictionary<string, object>
                {
                    ["CorrelationId"] = correlationId
                }))
            {
                if (statusCode >= 500)
                {
                    _logger.LogError(
                        "HTTP {Method} {Path} responded {StatusCode} " +
                        "in {ElapsedMs:F2} ms. CorrelationId: {CorrelationId}",
                        method,
                        path,
                        statusCode,
                        elapsedMs,
                        correlationId);
                }
                else if (statusCode >= 400)
                {
                    _logger.LogWarning(
                        "HTTP {Method} {Path} responded {StatusCode} " +
                        "in {ElapsedMs:F2} ms. CorrelationId: {CorrelationId}",
                        method,
                        path,
                        statusCode,
                        elapsedMs,
                        correlationId);
                }
                else
                {
                    _logger.LogInformation(
                        "HTTP {Method} {Path} responded {StatusCode} " +
                        "in {ElapsedMs:F2} ms. CorrelationId: {CorrelationId}",
                        method,
                        path,
                        statusCode,
                        elapsedMs,
                        correlationId);
                }
            }
        }
    }
}
