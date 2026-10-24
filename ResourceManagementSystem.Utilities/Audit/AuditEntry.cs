namespace ResourceManagementSystem.Utilities;

public sealed class AuditEntry
{
    public int? StaffId { get; set; }
    public string? Role { get; set; }
    public string? BranchCode { get; set; }
    public string? Controller { get; set; }
    public string? Action { get; set; }
    public string? HttpMethod { get; set; }
    public string? Path { get; set; }
    public string? CorrelationId { get; set; }
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public Dictionary<string, string> RouteValues { get; set; } = new();
    public DateTime LoggedAtUtc { get; set; } = DateTime.UtcNow;
}
