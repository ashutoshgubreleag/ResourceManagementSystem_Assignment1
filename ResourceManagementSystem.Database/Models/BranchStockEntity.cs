namespace ResourceManagementSystem.Database.Models;

public class BranchStockEntity
{
    public int Id { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int QuantityOnHand { get; set; }
    public int ReorderThreshold { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
