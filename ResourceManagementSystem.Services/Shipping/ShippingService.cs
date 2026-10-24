using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Repositories;

namespace ResourceManagementSystem.Services.Shipping;

public class ShippingService : IShippingService
{
    private readonly IBaseEntityRepository<ShippingOrderEntity> _shippingRepository;
    private readonly Dictionary<DestinationZone, decimal> _zoneRateMap = new()
    {
        [DestinationZone.Local] = 2.50m,
        [DestinationZone.Regional] = 4.00m,
        [DestinationZone.National] = 7.00m,
        [DestinationZone.International] = 12.00m
    };

    private readonly Dictionary<ShippingServiceLevel, decimal> _serviceMultipliers = new()
    {
        [ShippingServiceLevel.Standard] = 1.00m,
        [ShippingServiceLevel.Express] = 1.75m,
        [ShippingServiceLevel.Registered] = 2.25m
    };

    private readonly Dictionary<ShippingServiceLevel, decimal> _maxWeightByService = new()
    {
        [ShippingServiceLevel.Standard] = 5m,
        [ShippingServiceLevel.Express] = 3m,
        [ShippingServiceLevel.Registered] = 1m
    };

    public ShippingService(IBaseEntityRepository<ShippingOrderEntity> shippingRepository)
    {
        _shippingRepository = shippingRepository;
    }

    public decimal CalculatePrice(ShippingType shippingType, DestinationZone destinationZone, ShippingServiceLevel serviceLevel, decimal weightKg)
    {
        var baseRate = _zoneRateMap[destinationZone];
        var multiplier = _serviceMultipliers[serviceLevel];
        var weightFactor = Math.Max(weightKg, 1m);

        var price = (baseRate * multiplier) + (weightFactor * 0.75m);
        return Math.Round(price, 2, MidpointRounding.AwayFromZero);
    }

    public bool IsValidForServiceLevel(ShippingType shippingType, ShippingServiceLevel serviceLevel, decimal weightKg)
    {
        if (shippingType != ShippingType.Parcel) return true;
        return weightKg <= _maxWeightByService[serviceLevel];
    }

    public ShippingOrderEntity CreateShippingOrder(ShippingOrderEntity order)
    {
        if (order == null) throw new ArgumentNullException(nameof(order));
        if (order.ShippingType == ShippingType.Parcel && !IsValidForServiceLevel(order.ShippingType, order.ServiceLevel, order.WeightKg))
            throw new InvalidOperationException("Parcel exceeds the maximum weight for the selected service level.");

        order.CalculatedPrice = CalculatePrice(order.ShippingType, order.DestinationZone, order.ServiceLevel, order.WeightKg);
        return _shippingRepository.Add(order);
    }
}
