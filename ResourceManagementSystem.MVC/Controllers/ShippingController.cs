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
        return View(new ShippingOrderEntity
        {
            ShippingType = ShippingType.Parcel,
            DestinationZone = DestinationZone.Local,
            ServiceLevel = ShippingServiceLevel.Standard,
            LengthCm = 10m,
            WidthCm = 10m,
            HeightCm = 10m,
            WeightKg = 1m
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Calculate(ShippingOrderEntity model)
    {
        try
        {
            if (model.LengthCm <= 0m || model.WidthCm <= 0m || model.HeightCm <= 0m)
            {
                model.LengthCm = 10m;
                model.WidthCm = 10m;
                model.HeightCm = 10m;
            }

            model.CalculatedPrice = _shippingService.CalculatePrice(
                model.ShippingType,
                model.DestinationZone,
                model.ServiceLevel,
                model.WeightKg,
                model.LengthCm,
                model.WidthCm,
                model.HeightCm);

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
