using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ResourceManagementSystem.Utilities.Options;

namespace ResourceManagementSystem.Utilities.Middleware;

/// <summary>
/// Validates <c>X-Api-Key</c> on endpoints marked <see cref="ApiKeyRequiredAttribute"/>.
/// <list type="bullet">
/// <item>Keys come from configuration (user-secrets / environment), never source code.</item>
/// <item>500 when no partner key is configured: a deployment error must not look like a bad key, and
/// must never fail open.</item>
/// <item>401 for a missing or wrong key.</item>
/// <item>Constant-time comparison: both values are hashed to fixed-length digests and compared with
/// <see cref="CryptographicOperations.FixedTimeEquals"/>, and every configured key is checked (no early
/// exit), so timing reveals neither the key nor which partner matched.</item>
/// </list>
/// On success the partner id is stored in <c>HttpContext.Items</c> for the per-partner rate limiter.
/// </summary>
public sealed partial class ApiKeyValidationMiddleware(
    RequestDelegate next,
    IOptionsMonitor<IntegrationOptions> options,
    ILogger<ApiKeyValidationMiddleware> logger)
{
    public const string PartnerItemKey = "IntegrationPartnerId";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.GetEndpoint()?.Metadata.GetMetadata<ApiKeyRequiredAttribute>() is null)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var partners = options.CurrentValue.Partners
            .Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .ToList();

        if (partners.Count == 0)
        {
            LogNotConfigured(context.Request.Path.Value ?? "/");
            await ProblemResponses.WriteAsync(context, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Integration not configured",
                Detail = "The integration endpoint is not available. Contact ApexPost support.",
            }).ConfigureAwait(false);
            return;
        }

        var supplied = context.Request.Headers[IntegrationOptions.ApiKeyHeaderName].ToString();
        var partnerId = string.IsNullOrEmpty(supplied) ? null : Match(supplied, partners);
        if (partnerId is null)
        {
            LogRejected(context.Request.Path.Value ?? "/", string.IsNullOrEmpty(supplied) ? "missing" : "invalid");
            context.Response.Headers.WWWAuthenticate = "ApiKey header=\"X-Api-Key\"";
            await ProblemResponses.WriteAsync(context, new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "A valid X-Api-Key header is required.",
            }).ConfigureAwait(false);
            return;
        }

        context.Items[PartnerItemKey] = partnerId;
        await next(context).ConfigureAwait(false);
    }

    private static string? Match(string supplied, List<KeyValuePair<string, string>> partners)
    {
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        string? matched = null;
        foreach (var (partner, key) in partners)
        {
            var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
            if (CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash))
            {
                matched = partner; // keep looping: no early exit
            }
        }

        return matched;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Integration API key is not configured; rejected request to {Path}")]
    private partial void LogNotConfigured(string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rejected integration request to {Path}: API key {Reason}")]
    private partial void LogRejected(string path, string reason);
}
