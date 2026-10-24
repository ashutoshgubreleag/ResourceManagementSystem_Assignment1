
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;

namespace ResourceManagementSystem.Utilities;

/// <summary>
/// Restricts configured administrative and integration endpoints
/// to clients whose IP addresses appear in the configured allowlist.
/// </summary>
/// <remarks>
/// RemoteIpAddress is only trustworthy when the application is directly
/// exposed to clients or forwarded headers are configured to trust only
/// known reverse proxies. Never trust arbitrary X-Forwarded-For values.
/// </remarks>
public sealed class IpRestrictionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IpRestrictionMiddleware> _logger;
    private readonly bool _enabled;
    private readonly HashSet<IPAddress> _allowedIPs;
    private readonly PathString[] _protectedPaths;

    public IpRestrictionMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<IpRestrictionMiddleware> logger)
    {
        _next = next;
        _logger = logger;

        var section = configuration.GetSection("IpRestriction");

        _enabled = section.GetValue<bool>("Enabled");

        var configuredIPs =
            section.GetSection("AllowedIPs").Get<string[]>() ?? [];

        _allowedIPs = new HashSet<IPAddress>();

        foreach (var value in configuredIPs)
        {
            if (!IPAddress.TryParse(value, out var address))
            {
                throw new InvalidOperationException(
                    $"Invalid IP address in IpRestriction: '{value}'.");
            }

            _allowedIPs.Add(address.MapToIPv6());
        }

        var paths = section
            .GetSection("ProtectedPathPrefixes")
            .Get<string[]>() ?? [];

        _protectedPaths = paths
            .Select(path => new PathString(path))
            .ToArray();

        if (_enabled && _allowedIPs.Count == 0)
        {
            throw new InvalidOperationException(
                "IP restriction is enabled, but AllowedIPs is empty.");
        }

        if (_enabled && _protectedPaths.Length == 0)
        {
            throw new InvalidOperationException(
                "IP restriction is enabled, but no protected paths are configured.");
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_enabled || !IsProtectedPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var clientIP = context.Connection.RemoteIpAddress;

        if (clientIP is null ||
            !_allowedIPs.Contains(clientIP.MapToIPv6()))
        {
            _logger.LogWarning(
                "IP restriction denied request. Path: {Path}; Client IP: {ClientIP}",
                context.Request.Path,
                clientIP?.ToString() ?? "Unknown");

            context.Response.StatusCode =
                StatusCodes.Status403Forbidden;

            return;
        }

        await _next(context);
    }

    private bool IsProtectedPath(PathString path)
    {
        return _protectedPaths.Any(prefix =>
            path.StartsWithSegments(
                prefix,
                StringComparison.OrdinalIgnoreCase));
    }
}
