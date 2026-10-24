namespace ResourceManagementSystem.Database.Models;

public class ProductEntity
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ProductCategory Category { get; set; }
    public decimal UnitPrice { get; set; }
    public TaxCategory TaxCategory { get; set; }
    public bool IsActive { get; set; } = true;
}
