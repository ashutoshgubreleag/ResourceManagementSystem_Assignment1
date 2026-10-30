using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ApexPost.Models.Diagnostics;

namespace ApexPost.Utilities.Middleware;

/// <summary>
/// Reuses a well-formed incoming <c>X-Correlation-Id</c> (so a courier partner can trace a call
/// across both systems) or generates one, echoes it on the response, and opens a logging scope so
/// every log entry written while handling the request carries it.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var correlationId = ReadOrCreate(context.Request);

        context.Items[ItemKey] = correlationId;
        context.TraceIdentifier = correlationId;
        Activity.Current?.SetTag("correlation.id", correlationId);

        // OnStarting: headers can only be added before the response starts; this also covers
        // responses written by later middleware (401/403/429/503) and the exception handler.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope("CorrelationId:{CorrelationId}", correlationId))
        using (CorrelationContext.Begin(correlationId))
        {
            await next(context).ConfigureAwait(false);
        }
    }

    public static string Get(HttpContext context) =>
        context?.Items[ItemKey] as string ?? context?.TraceIdentifier ?? string.Empty;

    private static string ReadOrCreate(HttpRequest request)
    {
        var incoming = request.Headers[HeaderName].ToString();

        // Only accept a single, bounded, safe token: it ends up in logs and response headers,
        // so anything else (log injection, header splitting, huge values) is replaced.
        return incoming is { Length: > 0 and <= 64 } && incoming.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')
            ? incoming
            : Guid.NewGuid().ToString("N");
    }
}
