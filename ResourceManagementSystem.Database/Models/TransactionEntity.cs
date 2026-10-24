using System.Collections.Generic;

namespace ResourceManagementSystem.Database.Models;

public class TransactionEntity
{
    public int Id { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public int CounterTerminalId { get; set; }
    public int ClerkStaffId { get; set; }
    public List<TransactionLineItemEntity> LineItems { get; set; } = new();
    public PaymentMethod PaymentMethod { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Open;
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? VoidReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
}
