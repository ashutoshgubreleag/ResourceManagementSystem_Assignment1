using System.Globalization;
using System.Security.Cryptography;
using ApexPost.Database.Models;
using ApexPost.Models.Exceptions;
using ApexPost.Models.Validation;
using ApexPost.Models.ValueObjects;
using ApexPost.Repositories;
using ApexPost.Repositories.Specifications;
using ApexPost.Services.Caching;
using ApexPost.Services.Common;
using ApexPost.Services.Courier;

namespace ApexPost.Services.Shipping;

public sealed class ShippingService(
    IBaseEntityRepository<ShippingOrderEntity> shipments,
    IReferenceDataCache referenceData,
    ICourierRateService courierRates,
    IShippingPricingStrategy pricing,
    TimeProvider clock) : IShippingService
{
    public async Task<IReadOnlyList<ShippingZoneEntity>> GetZonesAsync(CancellationToken cancellationToken)
    {
        var zones = await referenceData.GetZonesAsync(cancellationToken).ConfigureAwait(false);
        return zones.Values.OrderBy(z => z.ZoneCode, StringComparer.Ordinal).ToList();
    }

    public async Task<ShippingQuote> QuoteAsync(ShippingQuoteCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ReflectionValidator.EnsureValid(command);

        var zones = await referenceData.GetZonesAsync(cancellationToken).ConfigureAwait(false);
        if (!zones.TryGetValue(command.ZoneCode, out var zone))
        {
            throw new CommandValidationException(nameof(command.ZoneCode), $"Unknown destination zone '{command.ZoneCode}'.");
        }

        var weight = new Weight(command.WeightKg);
        var dimensions = new Dimensions(command.LengthCm, command.WidthCm, command.HeightCm);
        ServiceLevelRules.EnsureAcceptable(command.ShippingType, command.ServiceLevel, weight, dimensions);

        // External call: bounded by the courier service's SemaphoreSlim and cancellable.
        var fuelRate = await courierRates.GetFuelSurchargeRateAsync(zone, cancellationToken).ConfigureAwait(false);

        return pricing.Price(new ShippingPricingContext(command.ShippingType, command.ServiceLevel, zone, weight, dimensions, fuelRate));
    }

    public async Task<ShippingOrderEntity> CreateShipmentAsync(int transactionId, BranchCode branch, ShippingQuote quote, Address recipient, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(quote);
        var now = clock.GetUtcNow();
        var shipment = new ShippingOrderEntity
        {
            TrackingNumber = string.Create(CultureInfo.InvariantCulture, $"APX{now:yyMMdd}{RandomNumberGenerator.GetInt32(100000, 999999)}"),
            TransactionId = transactionId,
            BranchCode = branch,
            ShippingType = quote.Context.ShippingType,
            ServiceLevel = quote.Context.ServiceLevel,
            ZoneCode = quote.Context.Zone.ZoneCode,
            Weight = quote.Context.ActualWeight,
            Dimensions = quote.Context.Dimensions,
            ChargeableWeight = quote.ChargeableWeight,
            Recipient = recipient,
            Price = quote.Net,
            Status = ShipmentStatus.Quoted,
            CreatedAt = now,
        };

        return await shipments.AddAsync(shipment, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ShippingOrderEntity> GetShipmentAsync(int shipmentId, StaffContext actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var shipment = await shipments.GetByIdAsync(shipmentId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException("Shipment", shipmentId);

        return actor.CanReadBranch(shipment.BranchCode)
            ? shipment
            : throw new ForbiddenOperationException("You can only view shipments from your own branch.");
    }

    public async Task SetStatusAsync(IReadOnlyCollection<int> shipmentIds, ShipmentStatus status, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(shipmentIds);
        if (shipmentIds.Count == 0)
        {
            return;
        }

        var targets = await shipments.FindAsync(Specification<ShippingOrderEntity>.Where(s => shipmentIds.Contains(s.Id)), cancellationToken).ConfigureAwait(false);
        foreach (var shipment in targets)
        {
            shipment.Status = status;
        }

        await shipments.UpdateRangeAsync(targets.ToList(), cancellationToken).ConfigureAwait(false);
    }
}
