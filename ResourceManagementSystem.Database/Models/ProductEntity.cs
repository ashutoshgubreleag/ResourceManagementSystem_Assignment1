using ResourceManagementSystem.Models.ValueObjects;

namespace ResourceManagementSystem.Database.Models;

public class ProductEntity : IEntity
{
    public int Id { get; set; }
    public Sku Sku { get; set; }
    public string Name { get; set => field = value?.Trim() ?? string.Empty; } = string.Empty;
    public ProductCategory Category { get; set; }

    /// <summary>Tax-exclusive shelf price.</summary>
    public Money UnitPrice { get; set; }

    public TaxCategory TaxCategory { get; set; }
    public bool IsActive { get; set; } = true;
}
