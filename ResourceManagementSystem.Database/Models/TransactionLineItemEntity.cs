namespace ResourceManagementSystem.Database.Models;

public class TransactionLineItemEntity
{
    public int Id { get; set; }
    public int TransactionId { get; set; }
    public TransactionLineType LineType { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
}
