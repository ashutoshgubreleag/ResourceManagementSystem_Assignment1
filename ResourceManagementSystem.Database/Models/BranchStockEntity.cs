using ResourceManagementSystem.Models.Exceptions;
using ResourceManagementSystem.Models.ValueObjects;

namespace ResourceManagementSystem.Database.Models;

/// <summary>Quantity of one SKU held at one branch.</summary>
public class BranchStockEntity : IEntity
{
    public int Id { get; set; }
    public BranchCode BranchCode { get; set; }
    public Sku Sku { get; set; }

    /// <summary>
    /// Last line of defence for "branch stock can never go negative": even if a service forgot
    /// to check, the setter (C# 14 <c>field</c> keyword) refuses a negative quantity.
    /// </summary>
    public int QuantityOnHand
    {
        get;
        set => field = value >= 0
            ? value
            : throw new BusinessRuleViolationException("STOCK_NEGATIVE", $"Stock for {Sku} at {BranchCode} cannot go below zero.");
    }

    public int ReorderThreshold { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
