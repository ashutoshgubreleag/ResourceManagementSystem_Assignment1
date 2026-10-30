using ApexPost.Models.Diagnostics;
using ApexPost.Models.ValueObjects;

namespace ApexPost.Services.Events;

/// <summary>Common data on every business event: where, when, and which request caused it.</summary>
public abstract class ApexPostEventArgs(BranchCode branchCode, DateTimeOffset occurredAt) : EventArgs
{
    public BranchCode BranchCode { get; } = branchCode;
    public DateTimeOffset OccurredAt { get; } = occurredAt;

    /// <summary>Captured at raise time so asynchronous handlers can still correlate.</summary>
    public string CorrelationId { get; } = CorrelationContext.CorrelationId ?? string.Empty;
}

public sealed class TransactionCompletedEventArgs(int transactionId, BranchCode branch, Money total, int clerkStaffId, DateTimeOffset at)
    : ApexPostEventArgs(branch, at)
{
    public int TransactionId { get; } = transactionId;
    public Money Total { get; } = total;
    public int ClerkStaffId { get; } = clerkStaffId;
}

public sealed class StockLowEventArgs(BranchCode branch, Sku sku, string productName, int quantityOnHand, int reorderThreshold, DateTimeOffset at)
    : ApexPostEventArgs(branch, at)
{
    public Sku Sku { get; } = sku;
    public string ProductName { get; } = productName;
    public int QuantityOnHand { get; } = quantityOnHand;
    public int ReorderThreshold { get; } = reorderThreshold;
}

public sealed class MoneyOrderIssuedEventArgs(int moneyOrderId, string serialNumber, BranchCode branch, Money amount, bool isHighValue, DateTimeOffset at)
    : ApexPostEventArgs(branch, at)
{
    public int MoneyOrderId { get; } = moneyOrderId;
    public string SerialNumber { get; } = serialNumber;
    public Money Amount { get; } = amount;
    public bool IsHighValue { get; } = isHighValue;
}

public sealed class TransactionVoidedEventArgs(int transactionId, BranchCode branch, int managerStaffId, string reason, bool isRefund, Money total, DateTimeOffset at)
    : ApexPostEventArgs(branch, at)
{
    public int TransactionId { get; } = transactionId;
    public int ManagerStaffId { get; } = managerStaffId;
    public string Reason { get; } = reason;
    public bool IsRefund { get; } = isRefund;
    public Money Total { get; } = total;
}

/// <summary>
/// Observer hub for business events. Services raise events; subscribers (the notification
/// dispatcher today, a message bus later) react without the services knowing who listens.
/// Singleton, so subscriptions made at startup see events from every request.
/// </summary>
public interface IApexPostEvents
{
    event EventHandler<TransactionCompletedEventArgs>? TransactionCompleted;
    event EventHandler<StockLowEventArgs>? StockLow;
    event EventHandler<MoneyOrderIssuedEventArgs>? MoneyOrderIssued;

    /// <summary>Raised for voids and refunds (see <see cref="TransactionVoidedEventArgs.IsRefund"/>).</summary>
    event EventHandler<TransactionVoidedEventArgs>? TransactionVoided;

    void Publish(TransactionCompletedEventArgs args);
    void Publish(StockLowEventArgs args);
    void Publish(MoneyOrderIssuedEventArgs args);
    void Publish(TransactionVoidedEventArgs args);
}

public sealed class ApexPostEventHub : IApexPostEvents
{
    public event EventHandler<TransactionCompletedEventArgs>? TransactionCompleted;
    public event EventHandler<StockLowEventArgs>? StockLow;
    public event EventHandler<MoneyOrderIssuedEventArgs>? MoneyOrderIssued;
    public event EventHandler<TransactionVoidedEventArgs>? TransactionVoided;

    public void Publish(TransactionCompletedEventArgs args) => TransactionCompleted?.Invoke(this, args);
    public void Publish(StockLowEventArgs args) => StockLow?.Invoke(this, args);
    public void Publish(MoneyOrderIssuedEventArgs args) => MoneyOrderIssued?.Invoke(this, args);
    public void Publish(TransactionVoidedEventArgs args) => TransactionVoided?.Invoke(this, args);
}
