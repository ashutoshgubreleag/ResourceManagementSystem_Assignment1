using System.Collections.Generic;

namespace ResourceManagementSystem.Database.Models;

public class BranchEntity
{
    public int Id { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string OpeningHours { get; set; } = string.Empty;
    public BranchStatus Status { get; set; } = BranchStatus.Active;
    public List<CounterTerminalEntity> Counters { get; set; } = new();
}
