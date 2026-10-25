using Microsoft.Extensions.Caching.Memory;
using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Models.ValueObjects;
using ResourceManagementSystem.Repositories;

namespace ResourceManagementSystem.Services.Caching;

/// <summary>Frequently read, rarely changed reference data: the product catalogue and zone rate cards.</summary>
public interface IReferenceDataCache
{
    ValueTask<IReadOnlyDictionary<Sku, ProductEntity>> GetProductsAsync(CancellationToken cancellationToken);
    ValueTask<IReadOnlyDictionary<string, ShippingZoneEntity>> GetZonesAsync(CancellationToken cancellationToken);
    void InvalidateProducts();
}

/// <summary>
/// Caches indexed reference data in <see cref="IMemoryCache"/>.
///
/// Why ValueTask: every basket line and every shipping quote needs a product or zone lookup.
/// On a cache hit (~all calls after the first) the method returns synchronously, and
/// <see cref="ValueTask{TResult}"/> lets it do so without allocating a Task. Only the rare miss
/// pays for the async load. Why a cache on top of the repository: the repository hands out
/// deep copies of every row (safe, but it re-serialises each time); the cache builds an O(1)
/// dictionary once and shares immutable-by-convention instances. DESIGN.md has the measurements.
/// </summary>
public sealed class ReferenceDataCache(
    IMemoryCache cache,
    IBaseEntityRepository<ProductEntity> products,
    IBaseEntityRepository<ShippingZoneEntity> zones) : IReferenceDataCache
{
    private const string ProductsKey = "ref:products";
    private const string ZonesKey = "ref:zones";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    public ValueTask<IReadOnlyDictionary<Sku, ProductEntity>> GetProductsAsync(CancellationToken cancellationToken) =>
        cache.TryGetValue(ProductsKey, out IReadOnlyDictionary<Sku, ProductEntity>? hit) && hit is not null
            ? ValueTask.FromResult(hit)
            : new ValueTask<IReadOnlyDictionary<Sku, ProductEntity>>(LoadProductsAsync(cancellationToken));

    public ValueTask<IReadOnlyDictionary<string, ShippingZoneEntity>> GetZonesAsync(CancellationToken cancellationToken) =>
        cache.TryGetValue(ZonesKey, out IReadOnlyDictionary<string, ShippingZoneEntity>? hit) && hit is not null
            ? ValueTask.FromResult(hit)
            : new ValueTask<IReadOnlyDictionary<string, ShippingZoneEntity>>(LoadZonesAsync(cancellationToken));

    public void InvalidateProducts() => cache.Remove(ProductsKey);

    private async Task<IReadOnlyDictionary<Sku, ProductEntity>> LoadProductsAsync(CancellationToken cancellationToken)
    {
        var all = await products.GetAllAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<Sku, ProductEntity> index = all.ToDictionary(p => p.Sku);
        return cache.Set(ProductsKey, index, Lifetime);
    }

    private async Task<IReadOnlyDictionary<string, ShippingZoneEntity>> LoadZonesAsync(CancellationToken cancellationToken)
    {
        var all = await zones.GetAllAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<string, ShippingZoneEntity> index = all.ToDictionary(z => z.ZoneCode, StringComparer.OrdinalIgnoreCase);
        return cache.Set(ZonesKey, index, Lifetime);
    }
}
