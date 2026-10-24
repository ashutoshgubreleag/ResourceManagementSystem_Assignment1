using ResourceManagementSystem.Database.Models;

namespace ResourceManagementSystem.Services.Catalog;

public interface IProductCatalogService
{
    IEnumerable<ProductEntity> GetProducts();
    IEnumerable<BranchStockEntity> GetAllStock();
}
