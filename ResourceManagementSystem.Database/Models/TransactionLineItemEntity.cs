using ResourceManagementSystem.Models.ValueObjects;

namespace ResourceManagementSystem.Database.Models;

/// <summary>
/// One line of a basket. Composition over inheritance: every line has the same shape
/// (code, quantity, price, tax) and <see cref="LineType"/> + <see cref="ReferenceId"/> point at
/// the record behind it. This maps to a single table next week with no polymorphic JSON.
/// </summary>
public class TransactionLineItemEntity
{
    /// <summary>Line number within the transaction (1, 2, 3...).</summary>
    public int Id { get; set; }

    public TransactionLineType LineType { get; set; }

    /// <summary>SKU, tracking number, money order serial or "IDV".</summary>
    public string ItemCode { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public Money UnitPrice { get; set; }
    public TaxCategory TaxCategory { get; set; }
    public Money TaxAmount { get; set; }

    /// <summary>Tax-exclusive net amount (UnitPrice x Quantity).</summary>
    public Money LineTotal { get; set; }

    /// <summary>Non-revenue money collected by this line (money order face value).</summary>
    public Money PassThroughAmount { get; set; }

    /// <summary>Id of the shipment, money order or verification behind a service line.</summary>
    public int? ReferenceId { get; set; }
}
