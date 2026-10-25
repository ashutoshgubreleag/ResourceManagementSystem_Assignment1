using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ResourceManagementSystem.Utilities.Middleware;

/// <summary>
/// Structured request log plus timing. The log is written in <c>finally</c> so requests that throw
/// are timed and logged too. It sits outside the exception handler, so it records the final status
/// (500) that the handler wrote rather than the 200 the response had when the exception occurred.
/// </summary>
public sealed partial class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var started = Stopwatch.GetTimestamp();
        var failed = false;
        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch
        {
            failed = true;
            throw;
        }
        finally
        {
            var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var status = failed ? StatusCodes.Status500InternalServerError : context.Response.StatusCode;
            var staffId = context.User.FindFirst("StaffId")?.Value ?? "anonymous";
            var level = status switch
            {
                >= 500 => LogLevel.Error,
                >= 400 => LogLevel.Warning,
                _ => LogLevel.Information,
            };

            // Path only: query strings can contain personal data and are not logged.
            LogRequest(level, context.Request.Method, context.Request.Path.Value ?? "/", status, Math.Round(elapsedMs, 2), staffId);
        }
    }

    [LoggerMessage(Message = "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs} ms (staff {StaffId})")]
    private partial void LogRequest(LogLevel level, string method, string path, int statusCode, double elapsedMs, string staffId);
}
