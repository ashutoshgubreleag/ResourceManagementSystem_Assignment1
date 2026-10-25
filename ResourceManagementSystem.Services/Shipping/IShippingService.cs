using ResourceManagementSystem.Database.Models;

namespace ResourceManagementSystem.Services.Shipping;

public interface IShippingService
{
    decimal CalculatePrice(ShippingType shippingType, DestinationZone destinationZone, ShippingServiceLevel serviceLevel, decimal weightKg, decimal lengthCm = 0m, decimal widthCm = 0m, decimal heightCm = 0m);
    bool IsValidForServiceLevel(ShippingType shippingType, ShippingServiceLevel serviceLevel, decimal weightKg, decimal lengthCm = 0m, decimal widthCm = 0m, decimal heightCm = 0m);
    ShippingOrderEntity CreateShippingOrder(ShippingOrderEntity order);
}
