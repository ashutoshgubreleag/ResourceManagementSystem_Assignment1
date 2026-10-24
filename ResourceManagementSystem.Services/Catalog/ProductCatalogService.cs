using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Repositories;

namespace ResourceManagementSystem.Services.Catalog;

public class ProductCatalogService : IProductCatalogService
{
    private readonly IBaseEntityRepository<ProductEntity> _productRepository;
    private readonly IBaseEntityRepository<BranchStockEntity> _stockRepository;

    public ProductCatalogService(
        IBaseEntityRepository<ProductEntity> productRepository,
        IBaseEntityRepository<BranchStockEntity> stockRepository)
    {
        _productRepository = productRepository;
        _stockRepository = stockRepository;
    }

    public IEnumerable<ProductEntity> GetProducts() => _productRepository.GetAll();

    public IEnumerable<BranchStockEntity> GetAllStock() => _stockRepository.GetAll();
}
