using ApexPost.Models.ValueObjects;

namespace ApexPost.Database.Models;

public class BranchEntity : IEntity
{
    public int Id { get; set; }

    public BranchCode BranchCode { get; set; }

    // C# 14 `field` keyword: trimming/guarding without declaring a private backing field.
    public string Name
    {
        get;
        set => field = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Branch name is required.", nameof(Name)) : value.Trim();
    } = "Unnamed branch";

    public string Region { get; set => field = value?.Trim() ?? string.Empty; } = string.Empty;

    /// <summary>Human-readable opening hours, e.g. "Mon-Fri 08:30-17:30; Sat 09:00-12:00".</summary>
    public string OpeningHours { get; set => field = value?.Trim() ?? string.Empty; } = string.Empty;

    public BranchStatus Status { get; set; } = BranchStatus.Active;

    public List<CounterTerminalEntity> Counters { get; set; } = [];
}
