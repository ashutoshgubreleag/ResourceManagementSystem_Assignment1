namespace ResourceManagementSystem.Database.Models;

public class CounterTerminalEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string BranchCode { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
