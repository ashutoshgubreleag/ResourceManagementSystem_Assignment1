namespace ResourceManagementSystem.Models.ViewModels;

public class TransactionRequestModel
{
    public string BranchCode { get; set; } = string.Empty;
    public int CounterTerminalId { get; set; }
    public int ClerkStaffId { get; set; }
    public List<TransactionLineRequestModel> LineItems { get; set; } = new();
}

public class TransactionLineRequestModel
{
    public string ItemCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Type { get; set; } = "RetailItem";
}
