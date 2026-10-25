namespace ResourceManagementSystem.Utilities.Options;

/// <summary>Section <c>MaintenanceMode</c>. Read through IOptionsMonitor, so it can be flipped without a restart.</summary>
public sealed class MaintenanceModeOptions
{
    public const string SectionName = "MaintenanceMode";

    public bool Enabled { get; set; }
    public int RetryAfterSeconds { get; set; } = 600;
    public string Message { get; set; } = "ApexPost Counter is undergoing scheduled maintenance. Please try again shortly.";

    /// <summary>Paths that stay available during maintenance (health probes).</summary>
    public string[] AlwaysAvailablePaths { get; set; } = ["/health"];
}

/// <summary>Section <c>IpRestriction</c>.</summary>
public sealed class IpRestrictionOptions
{
    public const string SectionName = "IpRestriction";

    public bool Enabled { get; set; } = true;

    /// <summary>Single addresses ("10.0.0.5") or CIDR ranges ("10.20.0.0/16").</summary>
    public string[] AllowList { get; set; } = ["127.0.0.1", "::1"];

    public string[] ProtectedPathPrefixes { get; set; } = ["/admin", "/integration"];
}

/// <summary>
/// Section <c>Integration</c>. Partner keys come from user-secrets or environment variables
/// (<c>Integration__Partners__swiftcourier</c>), never from appsettings.json in source control.
/// </summary>
public sealed class IntegrationOptions
{
    public const string SectionName = "Integration";

    public const string ApiKeyHeaderName = "X-Api-Key";

    /// <summary>Partner id → API key.</summary>
    public Dictionary<string, string> Partners { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Section <c>Performance</c>.</summary>
public sealed class PerformanceOptions
{
    public const string SectionName = "Performance";

    public int SlowActionThresholdMs { get; set; } = 500;
}

/// <summary>Section <c>Audit</c>.</summary>
public sealed class AuditOptions
{
    public const string SectionName = "Audit";

    /// <summary>JSON Lines file; resolved against the data directory when relative.</summary>
    public string FilePath { get; set; } = "audit-log.jsonl";
}
