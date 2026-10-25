using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Models.ValueObjects;
using ResourceManagementSystem.Services.Common;
using ResourceManagementSystem.Services.MoneyOrders;

namespace ResourceManagementSystem.Services.Transactions;

/// <summary>Counter transactions: open a basket, add goods and services, take payment, void or refund.</summary>
public interface ITransactionService
{
    Task<IReadOnlyList<TransactionEntity>> GetRecentAsync(StaffContext actor, BranchCode? branch, int take, CancellationToken cancellationToken);

    Task<TransactionDetails> GetDetailsAsync(int transactionId, StaffContext actor, CancellationToken cancellationToken);

    Task<TransactionEntity> OpenAsync(StaffContext actor, int counterTerminalId, CancellationToken cancellationToken);

    Task<TransactionEntity> AddRetailItemAsync(int transactionId, AddRetailItemCommand command, StaffContext actor, CancellationToken cancellationToken);

    Task<ShippingOrderEntity> AddShipmentAsync(int transactionId, AddShipmentCommand command, StaffContext actor, CancellationToken cancellationToken);

    Task<MoneyOrderEntity> AddMoneyOrderAsync(int transactionId, MoneyOrderCommand command, StaffContext actor, CancellationToken cancellationToken);

    Task<IdentityVerificationEntity> AddIdentityVerificationAsync(int transactionId, IdentityVerificationCommand command, StaffContext actor, CancellationToken cancellationToken);

    Task<TransactionEntity> RemoveLineAsync(int transactionId, int lineNumber, StaffContext actor, CancellationToken cancellationToken);

    Task<TransactionEntity> PayAsync(int transactionId, PaymentMethod paymentMethod, StaffContext actor, CancellationToken cancellationToken);

    /// <summary>Branch Manager only, reason required.</summary>
    Task<TransactionEntity> VoidAsync(int transactionId, VoidOrRefundCommand command, StaffContext actor, CancellationToken cancellationToken);

    /// <summary>Branch Manager only, reason required, paid transactions only.</summary>
    Task<TransactionEntity> RefundAsync(int transactionId, VoidOrRefundCommand command, StaffContext actor, CancellationToken cancellationToken);
}
