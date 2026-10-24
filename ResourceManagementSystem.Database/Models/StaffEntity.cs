namespace ResourceManagementSystem.Database.Models;

public class StaffEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public StaffRole Role { get; set; }
    public string HomeBranchCode { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
