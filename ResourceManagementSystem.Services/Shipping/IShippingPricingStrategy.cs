namespace ResourceManagementSystem.Services.Shipping;

/// <summary>
/// Strategy pattern for shipping prices. The service owns the workflow (validate, look up the
/// zone, ask the courier for the fuel rate); the strategy owns the arithmetic, so a promotional
/// or partner-specific strategy can be registered without touching the service.
/// </summary>
public interface IShippingPricingStrategy
{
    ShippingQuote Price(ShippingPricingContext context);
}
