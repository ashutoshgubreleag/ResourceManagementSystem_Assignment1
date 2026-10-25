using ResourceManagementSystem.Database.Models;

namespace ResourceManagementSystem.Services.Shipping;

public interface IShippingPricingStrategy
{
    decimal CalculatePrice(ShippingOrderEntity order);
}