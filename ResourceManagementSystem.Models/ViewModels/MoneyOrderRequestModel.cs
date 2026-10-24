namespace ResourceManagementSystem.Models.ViewModels;

public class MoneyOrderRequestModel
{
    public string BranchCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Sender { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
}
