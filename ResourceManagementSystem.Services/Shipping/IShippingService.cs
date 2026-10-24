using ResourceManagementSystem.Database.Models;

namespace ResourceManagementSystem.Services.Shipping;

public interface IShippingService
{
    decimal CalculatePrice(ShippingType shippingType, DestinationZone destinationZone, ShippingServiceLevel serviceLevel, decimal weightKg);
    bool IsValidForServiceLevel(ShippingType shippingType, ShippingServiceLevel serviceLevel, decimal weightKg);
    ShippingOrderEntity CreateShippingOrder(ShippingOrderEntity order);
}
