using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Services.Shipping;

namespace ResourceManagementSystem.MVC.Controllers;

[Authorize]
public class ShippingController : Controller
{
    private readonly IShippingService _shippingService;

    public ShippingController(IShippingService shippingService)
    {
        _shippingService = shippingService;
    }

    public IActionResult Index()
    {
        return View(new ShippingOrderEntity());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Calculate(ShippingOrderEntity model)
    {
        try
        {
            model.CalculatedPrice = _shippingService.CalculatePrice(
                model.ShippingType,
                model.DestinationZone,
                model.ServiceLevel,
                model.WeightKg);

            _shippingService.CreateShippingOrder(model);
            return View("Index", model);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("Index", model);
        }
    }
}
