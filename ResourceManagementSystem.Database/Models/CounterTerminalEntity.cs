namespace ResourceManagementSystem.Database.Models;

/// <summary>A counter terminal inside a branch. Owned by <see cref="BranchEntity"/>; Id is the counter number.</summary>
public class CounterTerminalEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
