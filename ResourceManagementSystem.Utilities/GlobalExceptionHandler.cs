
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ResourceManagementSystem.Utilities;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = System.Diagnostics.Activity.Current?
            .TraceId.ToString()
            ?? httpContext.TraceIdentifier;

        var correlationId =
            httpContext.Items[CorrelationIdMiddleware.ItemKey]?.ToString()
            ?? httpContext.TraceIdentifier;

        //Log full exception details internally, never in the response.
        _logger.LogError(
            exception,
            "Unhandled exception. CorrelationId: {CorrelationId} and TraceId: {TraceId}",
            correlationId, traceId);

        var problemDetails = new ProblemDetails
        {
            Type = "https://httpstatuses.com/500",
            Title = "Internal Server Error",
            Status = StatusCodes.Status500InternalServerError,
            Detail = "An unexpected error occurred. Please try again later."
        };

        problemDetails.Extensions["traceId"] = traceId;
        problemDetails.Extensions["correlationId"] = correlationId;

        httpContext.Response.StatusCode =
            StatusCodes.Status500InternalServerError;

        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            cancellationToken);

        return true;
    }
}
