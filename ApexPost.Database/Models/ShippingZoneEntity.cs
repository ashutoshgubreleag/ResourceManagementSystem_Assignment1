using ApexPost.Models.ValueObjects;

namespace ApexPost.Database.Models;

/// <summary>Destination zone and its rate card. Read on every quote, so it is cached by the service layer.</summary>
public class ShippingZoneEntity : IEntity
{
    public int Id { get; set; }
    public string ZoneCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsInternational { get; set; }
    public bool IsRemote { get; set; }
    public Money LetterBaseRate { get; set; }
    public Money ParcelBaseRate { get; set; }
    public Money PerKgRate { get; set; }
}
