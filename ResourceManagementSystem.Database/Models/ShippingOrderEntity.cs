namespace ResourceManagementSystem.Database.Models;

public class ShippingOrderEntity
{
    public int Id { get; set; }
    public int TransactionId { get; set; }
    public ShippingType ShippingType { get; set; }
    public decimal WeightKg { get; set; }
    public string Dimensions { get; set; } = string.Empty;
    public DestinationZone DestinationZone { get; set; }
    public ShippingServiceLevel ServiceLevel { get; set; }
    public decimal CalculatedPrice { get; set; }
    public string Status { get; set; } = "Created";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
