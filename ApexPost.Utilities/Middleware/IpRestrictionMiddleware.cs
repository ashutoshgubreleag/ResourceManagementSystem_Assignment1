using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ApexPost.Utilities.Options;

namespace ApexPost.Utilities.Middleware;

/// <summary>
/// Allows admin and integration endpoints only from allowlisted addresses (single IPs or CIDR
/// ranges) and returns 403 otherwise. An endpoint is protected if its path starts with a configured
/// prefix or it carries <see cref="IpRestrictedAttribute"/>.
///
/// <para><b>Proxy risk:</b> this reads <c>Connection.RemoteIpAddress</c> only, never
/// <c>X-Forwarded-For</c> directly, because that header is set by the client and trivially spoofed.
/// Behind a reverse proxy, enable <c>ForwardedHeaders</c> with <c>KnownProxies</c>/<c>KnownNetworks</c>
/// set to the proxy's addresses: the framework then rewrites RemoteIpAddress only for requests that
/// really came through that proxy. Without that, every request appears to come from the proxy.</para>
/// </summary>
public sealed partial class IpRestrictionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IpRestrictionMiddleware> _logger;
    private readonly bool _enabled;
    private readonly IPNetwork[] _allowed;
    private readonly PathString[] _protectedPrefixes;

    public IpRestrictionMiddleware(RequestDelegate next, IOptions<IpRestrictionOptions> options, ILogger<IpRestrictionMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _next = next;
        _logger = logger;
        var value = options.Value;
        _enabled = value.Enabled;
        _allowed = value.AllowList.Select(ParseEntry).ToArray();
        _protectedPrefixes = value.ProtectedPathPrefixes.Select(p => new PathString(p)).ToArray();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!_enabled || !IsProtected(context))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var remote = context.Connection.RemoteIpAddress;
        if (remote is not null && remote.IsIPv4MappedToIPv6)
        {
            remote = remote.MapToIPv4();
        }

        if (remote is null || !_allowed.Any(n => n.Contains(remote)))
        {
            LogBlocked(context.Request.Path.Value ?? "/", remote?.ToString() ?? "unknown");
            await ProblemResponses.WriteAsync(context, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = "This endpoint is not available from your network.",
            }).ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    private bool IsProtected(HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<IpRestrictedAttribute>() is not null
        || _protectedPrefixes.Any(p => context.Request.Path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));

    private static IPNetwork ParseEntry(string entry)
    {
        if (IPNetwork.TryParse(entry, out var network))
        {
            return network;
        }

        if (IPAddress.TryParse(entry, out var address))
        {
            if (address.IsIPv4MappedToIPv6)
            {
                address = address.MapToIPv4();
            }

            return new IPNetwork(address, address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128);
        }

        throw new InvalidOperationException($"IpRestriction:AllowList contains an invalid entry '{entry}'.");
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "IP restriction blocked {Path} from {RemoteIp}")]
    private partial void LogBlocked(string path, string remoteIp);
}
