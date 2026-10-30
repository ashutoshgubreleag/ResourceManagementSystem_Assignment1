using ApexPost.Database.Models;
using ApexPost.Models.ValueObjects;

namespace ApexPost.Services.Catalog;

/// <summary>One catalogue row with the stock position at a branch.</summary>
public sealed record CatalogueItem(ProductEntity Product, int QuantityOnHand, int ReorderThreshold, bool IsLowStock);

public interface IProductCatalogService
{
    Task<IReadOnlyList<ProductEntity>> GetProductsAsync(CancellationToken cancellationToken);

    Task<ProductEntity?> FindProductAsync(Sku sku, CancellationToken cancellationToken);

    Task<IReadOnlyList<CatalogueItem>> GetCatalogueForBranchAsync(BranchCode branch, ProductCategory? category, CancellationToken cancellationToken);
}
