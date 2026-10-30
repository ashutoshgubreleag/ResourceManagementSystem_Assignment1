using ApexPost.Database.Models;
using ApexPost.Models.ValueObjects;
using ApexPost.Services.Caching;
using ApexPost.Services.Common;
using ApexPost.Services.Inventory;

namespace ApexPost.Services.Catalog;

public sealed class ProductCatalogService(
    IReferenceDataCache referenceData,
    IInventoryService inventory,
    BusinessRules rules) : IProductCatalogService
{
    public async Task<IReadOnlyList<ProductEntity>> GetProductsAsync(CancellationToken cancellationToken)
    {
        var products = await referenceData.GetProductsAsync(cancellationToken).ConfigureAwait(false);
        return products.Values.Where(p => p.IsActive).OrderBy(p => p.Category).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<ProductEntity?> FindProductAsync(Sku sku, CancellationToken cancellationToken)
    {
        var products = await referenceData.GetProductsAsync(cancellationToken).ConfigureAwait(false);
        return products.TryGetValue(sku, out var product) && product.IsActive ? product : null;
    }

    public async Task<IReadOnlyList<CatalogueItem>> GetCatalogueForBranchAsync(BranchCode branch, ProductCategory? category, CancellationToken cancellationToken)
    {
        // Independent reads: run them concurrently.
        var productsTask = GetProductsAsync(cancellationToken);
        var stockTask = inventory.GetStockForBranchAsync(branch, cancellationToken);
        await Task.WhenAll(productsTask, stockTask).ConfigureAwait(false);

        var stockBySku = (await stockTask.ConfigureAwait(false)).ToDictionary(s => s.Sku);
        return (await productsTask.ConfigureAwait(false))
            .Where(p => category is null || p.Category == category)
            .Select(p => stockBySku.TryGetValue(p.Sku, out var row)
                ? new CatalogueItem(p, row.QuantityOnHand, row.ReorderThreshold, rules.IsLowStock(row, rules.DefaultLowStockThreshold))
                : new CatalogueItem(p, 0, 0, true))
            .ToList();
    }
}
