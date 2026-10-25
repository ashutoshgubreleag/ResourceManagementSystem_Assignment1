using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Models.ValueObjects;
using ResourceManagementSystem.Services.Common;

namespace ResourceManagementSystem.Services.Shipping;

public interface IShippingService
{
    Task<IReadOnlyList<ShippingZoneEntity>> GetZonesAsync(CancellationToken cancellationToken);

    /// <summary>Validates the shipment against service-level limits and prices it.</summary>
    Task<ShippingQuote> QuoteAsync(ShippingQuoteCommand command, CancellationToken cancellationToken);

    /// <summary>Creates a shipment record for a basket line (status Quoted until the basket is paid).</summary>
    Task<ShippingOrderEntity> CreateShipmentAsync(int transactionId, BranchCode branch, ShippingQuote quote, Address recipient, CancellationToken cancellationToken);

    Task<ShippingOrderEntity> GetShipmentAsync(int shipmentId, StaffContext actor, CancellationToken cancellationToken);

    Task SetStatusAsync(IReadOnlyCollection<int> shipmentIds, ShipmentStatus status, CancellationToken cancellationToken);
}
