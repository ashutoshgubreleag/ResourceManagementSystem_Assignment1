using ApexPost.Models.ValueObjects;

namespace ApexPost.Database.Models;

/// <summary>A counter transaction (basket). Can mix retail items and services in <see cref="LineItems"/>.</summary>
public class TransactionEntity : IEntity
{
    public int Id { get; set; }
    public BranchCode BranchCode { get; set; }
    public int CounterTerminalId { get; set; }
    public int ClerkStaffId { get; set; }
    public List<TransactionLineItemEntity> LineItems { get; set; } = [];
    public PaymentMethod? PaymentMethod { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Open;

    /// <summary>Tax-exclusive revenue (retail + service fees).</summary>
    public Money Subtotal { get; set; }

    public Money TaxAmount { get; set; }

    /// <summary>Money order face values collected on behalf of recipients.</summary>
    public Money PassThroughAmount { get; set; }

    /// <summary>Amount the customer pays: subtotal + tax + pass-through.</summary>
    public Money TotalAmount { get; set; }

    /// <summary>Mandatory reason for a void or refund.</summary>
    public string? VoidReason { get; set; }

    public int? AuthorisedByStaffId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
}
