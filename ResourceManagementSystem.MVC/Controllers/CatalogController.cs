using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResourceManagementSystem.Services.Catalog;

namespace ResourceManagementSystem.MVC.Controllers;

[Authorize]
public class CatalogController : Controller
{
    private readonly IProductCatalogService _productCatalogService;

    public CatalogController(IProductCatalogService productCatalogService)
    {
        _productCatalogService = productCatalogService;
    }

    public IActionResult Index()
    {
        var products = _productCatalogService.GetProducts().ToList();
        var stocks = _productCatalogService.GetAllStock().ToList();

        var model = products.Select(product => new
        {
            product.Id,
            product.Sku,
            product.Name,
            product.Category,
            product.UnitPrice,
            product.TaxCategory,
            Quantity = stocks.Where(s => string.Equals(s.Sku, product.Sku, StringComparison.OrdinalIgnoreCase))
                .Sum(s => s.QuantityOnHand)
        }).ToList();

        return View(model);
    }
}
