using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ResourceManagementSystem.Utilities.Middleware;

/// <summary>Writes RFC 9457 Problem Details with trace and correlation IDs (and nothing internal).</summary>
public static class ProblemResponses
{
    public static Task WriteAsync(HttpContext context, ProblemDetails problem)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(problem);

        problem.Status ??= context.Response.StatusCode;
        problem.Type ??= $"https://httpstatuses.io/{problem.Status}";
        problem.Instance ??= context.Request.Path;
        problem.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        problem.Extensions["correlationId"] = CorrelationIdMiddleware.Get(context);

        context.Response.StatusCode = problem.Status.Value;
        return context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
}
