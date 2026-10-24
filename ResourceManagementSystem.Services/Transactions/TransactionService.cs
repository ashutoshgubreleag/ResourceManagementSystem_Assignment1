using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Repositories;
using ResourceManagementSystem.Services.Inventory;
using ResourceManagementSystem.Services.Staff;

namespace ResourceManagementSystem.Services.Transactions;

public class TransactionService : ITransactionService
{
    private readonly IBaseEntityRepository<TransactionEntity> _transactionRepository;
    private readonly IInventoryService _inventoryService;
    private readonly IStaffService _staffService;

    public TransactionService(
        IBaseEntityRepository<TransactionEntity> transactionRepository,
        IInventoryService inventoryService,
        IStaffService staffService)
    {
        _transactionRepository = transactionRepository;
        _inventoryService = inventoryService;
        _staffService = staffService;
    }

    public IEnumerable<TransactionEntity> GetAllTransactions() => _transactionRepository.GetAll();

    public TransactionEntity CreateTransaction(TransactionEntity transaction)
    {
        if (transaction == null) throw new ArgumentNullException(nameof(transaction));
        if (string.IsNullOrWhiteSpace(transaction.BranchCode)) throw new InvalidOperationException("Branch code is required.");
        if (transaction.ClerkStaffId <= 0) throw new InvalidOperationException("Clerk staff ID is required.");

        if (!_staffService.CanProcessTransaction(transaction.ClerkStaffId, transaction.BranchCode))
            throw new InvalidOperationException("Inactive staff or mismatched branch cannot process transactions.");

        transaction.Status = TransactionStatus.Open;
        transaction.CreatedAt = DateTime.UtcNow;
        return _transactionRepository.Add(transaction);
    }

    public TransactionEntity AddLineItem(TransactionEntity transaction, TransactionLineItemEntity item)
    {
        if (transaction == null) throw new ArgumentNullException(nameof(transaction));
        if (item == null) throw new ArgumentNullException(nameof(item));

        transaction.LineItems.Add(item);
        transaction.Subtotal += item.LineTotal;
        transaction.TaxAmount += item.TaxAmount;
        transaction.TotalAmount += item.LineTotal + item.TaxAmount;
        return transaction;
    }

    public TransactionEntity MarkPaid(int transactionId)
    {
        var transaction = _transactionRepository.GetById(transactionId)
            ?? throw new InvalidOperationException("Transaction not found.");

        foreach (var line in transaction.LineItems)
        {
            if (line.LineType == TransactionLineType.RetailItem)
            {
                _inventoryService.ReserveStock(transaction.BranchCode, line.ItemCode, line.Quantity);
            }
        }

        transaction.Status = TransactionStatus.Paid;
        transaction.ClosedAt = DateTime.UtcNow;
        var updated = _transactionRepository.Update(transaction);
        if (!updated) throw new InvalidOperationException("Unable to update transaction status.");
        return transaction;
    }

    public TransactionEntity VoidTransaction(int transactionId, int staffId, string reason, string branchCode)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Void reason is required.");
        if (!_staffService.CanVoidOrRefund(staffId, branchCode))
            throw new InvalidOperationException("Only a branch manager may void transactions.");

        var transaction = _transactionRepository.GetById(transactionId)
            ?? throw new InvalidOperationException("Transaction not found.");

        transaction.Status = TransactionStatus.Voided;
        transaction.VoidReason = reason;
        transaction.ClosedAt = DateTime.UtcNow;
        var updated = _transactionRepository.Update(transaction);
        if (!updated) throw new InvalidOperationException("Unable to void transaction.");
        return transaction;
    }

    public TransactionEntity RefundTransaction(int transactionId, int staffId, string reason, string branchCode)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Refund reason is required.");
        if (!_staffService.CanVoidOrRefund(staffId, branchCode))
            throw new InvalidOperationException("Only a branch manager may refund transactions.");

        var transaction = _transactionRepository.GetById(transactionId)
            ?? throw new InvalidOperationException("Transaction not found.");

        transaction.Status = TransactionStatus.Refunded;
        transaction.VoidReason = reason;
        transaction.ClosedAt = DateTime.UtcNow;
        var updated = _transactionRepository.Update(transaction);
        if (!updated) throw new InvalidOperationException("Unable to refund transaction.");
        return transaction;
    }

    public bool CanProcessTransaction(int staffId, string branchCode)
        => _staffService.CanProcessTransaction(staffId, branchCode);
}
