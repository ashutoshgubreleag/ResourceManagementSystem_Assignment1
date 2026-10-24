using ResourceManagementSystem.Database.Models;

namespace ResourceManagementSystem.Services.Transactions;

public interface ITransactionService
{
    TransactionEntity CreateTransaction(TransactionEntity transaction);
    TransactionEntity AddLineItem(TransactionEntity transaction, TransactionLineItemEntity item);
    TransactionEntity MarkPaid(int transactionId);
    TransactionEntity VoidTransaction(int transactionId, int staffId, string reason, string branchCode);
    TransactionEntity RefundTransaction(int transactionId, int staffId, string reason, string branchCode);
    bool CanProcessTransaction(int staffId, string branchCode);
}
