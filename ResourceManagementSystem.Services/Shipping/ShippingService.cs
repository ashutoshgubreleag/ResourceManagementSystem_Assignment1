using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Repositories;

namespace ResourceManagementSystem.Services.Shipping;

public class ShippingService : IShippingService
{
    private readonly IBaseEntityRepository<ShippingOrderEntity> _shippingRepository;
    private readonly IShippingPricingStrategy _pricingStrategy;
    private readonly Dictionary<ShippingServiceLevel, decimal> _maxWeightByService = new()
    {
        [ShippingServiceLevel.Standard] = 5m,
        [ShippingServiceLevel.Express] = 3m,
        [ShippingServiceLevel.Registered] = 1m
    };

    public ShippingService(
        IBaseEntityRepository<ShippingOrderEntity> shippingRepository,
        IShippingPricingStrategy pricingStrategy)
    {
        _shippingRepository = shippingRepository;
        _pricingStrategy = pricingStrategy ?? throw new ArgumentNullException(nameof(pricingStrategy));
    }

    public decimal CalculatePrice(ShippingType shippingType, DestinationZone destinationZone, ShippingServiceLevel serviceLevel, decimal weightKg, decimal lengthCm = 0m, decimal widthCm = 0m, decimal heightCm = 0m)
    {
        var order = new ShippingOrderEntity
        {
            ShippingType = shippingType,
            DestinationZone = destinationZone,
            ServiceLevel = serviceLevel,
            WeightKg = weightKg,
            LengthCm = lengthCm,
            WidthCm = widthCm,
            HeightCm = heightCm
        };

        return _pricingStrategy.CalculatePrice(order);
    }

    public bool IsValidForServiceLevel(ShippingType shippingType, ShippingServiceLevel serviceLevel, decimal weightKg, decimal lengthCm = 0m, decimal widthCm = 0m, decimal heightCm = 0m)
    {
        if (shippingType == ShippingType.Letter)
        {
            return weightKg <= 1m && lengthCm <= 35m && widthCm <= 25m && heightCm <= 5m;
        }

        return weightKg <= _maxWeightByService[serviceLevel];
    }

    public ShippingOrderEntity CreateShippingOrder(ShippingOrderEntity order)
    {
        if (order == null) throw new ArgumentNullException(nameof(order));
        if (order.WeightKg <= 0m)
            throw new InvalidOperationException("Weight must be greater than zero.");

        if (order.LengthCm <= 0m || order.WidthCm <= 0m || order.HeightCm <= 0m)
        {
            order.LengthCm = 10m;
            order.WidthCm = 10m;
            order.HeightCm = 10m;
        }

        if (!IsValidForServiceLevel(order.ShippingType, order.ServiceLevel, order.WeightKg, order.LengthCm, order.WidthCm, order.HeightCm))
            throw new InvalidOperationException("The selected shipment exceeds the service-level constraints.");

        order.CalculatedPrice = CalculatePrice(
            order.ShippingType,
            order.DestinationZone,
            order.ServiceLevel,
            order.WeightKg,
            order.LengthCm,
            order.WidthCm,
            order.HeightCm);

        return _shippingRepository.Add(order);
    }
}
