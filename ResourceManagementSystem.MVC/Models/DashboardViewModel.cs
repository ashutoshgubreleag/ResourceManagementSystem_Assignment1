namespace ResourceManagementSystem.MVC.Models;

public class DashboardViewModel
{
    public int BranchCount { get; set; }
    public int StaffCount { get; set; }
    public int ProductCount { get; set; }
    public int ActiveTransactionCount { get; set; }
    public int LowStockCount { get; set; }
}
