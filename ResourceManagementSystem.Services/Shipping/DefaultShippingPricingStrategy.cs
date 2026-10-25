using ResourceManagementSystem.Database.Models;

namespace ResourceManagementSystem.Services.Shipping;

public class DefaultShippingPricingStrategy : IShippingPricingStrategy
{
    private static readonly Dictionary<DestinationZone, decimal> ZoneRateMap = new()
    {
        [DestinationZone.Local] = 2.50m,
        [DestinationZone.Regional] = 4.00m,
        [DestinationZone.National] = 7.00m,
        [DestinationZone.International] = 12.00m
    };

    private static readonly Dictionary<ShippingServiceLevel, decimal> ServiceMultipliers = new()
    {
        [ShippingServiceLevel.Standard] = 1.00m,
        [ShippingServiceLevel.Express] = 1.75m,
        [ShippingServiceLevel.Registered] = 2.25m
    };

    public decimal CalculatePrice(ShippingOrderEntity order)
    {
        if (order == null) throw new ArgumentNullException(nameof(order));

        var baseRate = ZoneRateMap[order.DestinationZone];
        var multiplier = ServiceMultipliers[order.ServiceLevel];

        if (order.ShippingType == ShippingType.Letter)
        {
            var letterWeight = Math.Max(order.WeightKg, 0.25m);
            var price = (baseRate * multiplier) + (letterWeight * 0.35m);
            return Math.Round(price, 2, MidpointRounding.AwayFromZero);
        }

        var actualWeight = Math.Max(order.WeightKg, 0.25m);
        var volumetricWeight = order.VolumeCm3 > 0 ? order.VolumeCm3 / 5000m : 0m;
        var chargeableWeight = Math.Max(actualWeight, volumetricWeight);

        var parcelPrice = (baseRate * multiplier) + (chargeableWeight * 0.75m);
        return Math.Round(parcelPrice, 2, MidpointRounding.AwayFromZero);
    }
}