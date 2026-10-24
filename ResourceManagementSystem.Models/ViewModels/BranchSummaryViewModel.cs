namespace ResourceManagementSystem.Models.ViewModels;

public class BranchSummaryViewModel
{
    public string BranchCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public int ActiveStaffCount { get; set; }
    public int LowStockItemCount { get; set; }
}
