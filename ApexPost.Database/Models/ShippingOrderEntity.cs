using ApexPost.Models.ValueObjects;

namespace ApexPost.Database.Models;

/// <summary>A letter or parcel lodged as part of a counter transaction.</summary>
public class ShippingOrderEntity : IEntity
{
    public int Id { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public int TransactionId { get; set; }
    public BranchCode BranchCode { get; set; }
    public ShippingType ShippingType { get; set; }
    public Weight Weight { get; set; }
    public Dimensions Dimensions { get; set; }
    public Weight ChargeableWeight { get; set; }
    public string ZoneCode { get; set; } = string.Empty;
    public ShippingServiceLevel ServiceLevel { get; set; }
    public Address? Recipient { get; set; }

    /// <summary>Tax-exclusive postage.</summary>
    public Money Price { get; set; }

    public ShipmentStatus Status { get; set; } = ShipmentStatus.Quoted;
    public DateTimeOffset CreatedAt { get; set; }
}
