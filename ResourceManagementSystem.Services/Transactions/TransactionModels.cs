using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Models.Validation;
using ResourceManagementSystem.Models.ValueObjects;
using ResourceManagementSystem.Services.Shipping;

namespace ResourceManagementSystem.Services.Transactions;

public sealed record AddRetailItemCommand
{
    [NotBlank, Matches("^[A-Za-z]{3}-[0-9]{3}$", "a SKU such as PKG-001")]
    public string Sku { get; init; } = string.Empty;

    [InRange(1, 100)]
    public int Quantity { get; init; } = 1;
}

public sealed record AddShipmentCommand(ShippingQuoteCommand Quote, RecipientCommand Recipient);

public sealed record VoidOrRefundCommand
{
    [NotBlank, MaxLengthRule(200), Matches("^.{5,}$", "at least 5 characters")]
    public string Reason { get; init; } = string.Empty;
}

/// <summary>Everything the basket screen needs in one read.</summary>
public sealed record TransactionDetails(
    TransactionEntity Transaction,
    IReadOnlyList<ShippingOrderEntity> Shipments,
    IReadOnlyList<MoneyOrderEntity> MoneyOrders,
    IReadOnlyList<IdentityVerificationEntity> Verifications,
    bool IdentityVerificationRequired,
    bool IdentityVerificationSatisfied)
{
    /// <summary>Why the basket cannot be paid yet (null when it can).</summary>
    public string? PaymentBlocker =>
        Transaction.Status != TransactionStatus.Open ? "Transaction is not open."
        : Transaction.LineItems.Count == 0 ? "Add at least one item or service."
        : IdentityVerificationRequired && !IdentityVerificationSatisfied ? "A passed identity verification is required for the high-value money order."
        : null;
}

/// <summary>
/// C# 14 extension members giving the basket summary properties. The entity stays a plain
/// persistence model; these read like properties (<c>tx.RetailNet</c>) at every call site
/// (views, reports, dashboard) without duplicating the LINQ.
/// </summary>
public static class TransactionExtensions
{
    extension(TransactionEntity transaction)
    {
        public bool IsOpen => transaction.Status == TransactionStatus.Open;

        public int ItemCount => transaction.LineItems.Sum(l => l.Quantity);

        public Money RetailNet => transaction.LineItems
            .Where(l => l.LineType == TransactionLineType.RetailItem)
            .Select(l => l.LineTotal).Total();

        public Money ServicesNet => transaction.LineItems
            .Where(l => l.LineType != TransactionLineType.RetailItem)
            .Select(l => l.LineTotal).Total();

        public bool HasMoneyOrders => transaction.LineItems.Any(l => l.LineType == TransactionLineType.MoneyOrder);

        public int NextLineNumber => transaction.LineItems.Count == 0 ? 1 : transaction.LineItems.Max(l => l.Id) + 1;

        /// <summary>Recomputes the stored totals from the lines.</summary>
        public void RecalculateTotals()
        {
            transaction.Subtotal = transaction.LineItems.Select(l => l.LineTotal).Total();
            transaction.TaxAmount = transaction.LineItems.Select(l => l.TaxAmount).Total();
            transaction.PassThroughAmount = transaction.LineItems.Select(l => l.PassThroughAmount).Total();
            transaction.TotalAmount = transaction.Subtotal + transaction.TaxAmount + transaction.PassThroughAmount;
        }
    }
}
