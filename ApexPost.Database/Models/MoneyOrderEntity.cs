using ApexPost.Models.ValueObjects;

namespace ApexPost.Database.Models;

public class MoneyOrderEntity : IEntity
{
    public int Id { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public int TransactionId { get; set; }
    public BranchCode BranchCode { get; set; }

    /// <summary>Face value paid to the recipient (collected, but not ApexPost revenue).</summary>
    public Money Amount { get; set; }

    /// <summary>ApexPost's fee (revenue).</summary>
    public Money Fee { get; set; }

    public string Sender { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public int? IdentityVerificationId { get; set; }
    public MoneyOrderStatus Status { get; set; } = MoneyOrderStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? IssuedAt { get; set; }
}
