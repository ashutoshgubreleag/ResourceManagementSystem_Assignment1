namespace ResourceManagementSystem.Database.Models;

public class MoneyOrderEntity
{
    public int Id { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Sender { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public decimal Fee { get; set; }
    public int? IdentityVerificationId { get; set; }
    public MoneyOrderStatus Status { get; set; } = MoneyOrderStatus.Drafted;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
