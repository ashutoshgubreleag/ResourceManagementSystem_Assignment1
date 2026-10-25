using System.Globalization;
using System.Security.Cryptography;
using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Models.Exceptions;
using ResourceManagementSystem.Models.Validation;
using ResourceManagementSystem.Models.ValueObjects;
using ResourceManagementSystem.Repositories;
using ResourceManagementSystem.Repositories.Specifications;
using ResourceManagementSystem.Services.Common;
using ResourceManagementSystem.Services.Events;

namespace ResourceManagementSystem.Services.MoneyOrders;

public sealed class MoneyOrderService(
    IBaseEntityRepository<MoneyOrderEntity> moneyOrders,
    IBaseEntityRepository<IdentityVerificationEntity> verifications,
    BusinessRules rules,
    IApexPostEvents events,
    TimeProvider clock) : IMoneyOrderService
{
    public Money CalculateFee(Money amount) => rules.MoneyOrderFee(amount);

    public async Task<MoneyOrderEntity> CreatePendingAsync(int transactionId, BranchCode branch, MoneyOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ReflectionValidator.EnsureValid(command);

        var amount = new Money(command.Amount).RoundedToCents;
        if (amount > rules.MaximumMoneyOrderAmount)
        {
            throw new BusinessRuleViolationException("MONEY_ORDER_LIMIT", $"Money orders are limited to {rules.MaximumMoneyOrderAmount}.");
        }

        var now = clock.GetUtcNow();
        var order = new MoneyOrderEntity
        {
            SerialNumber = string.Create(CultureInfo.InvariantCulture, $"MO-{branch.Value[3..]}-{now:yyMMdd}-{RandomNumberGenerator.GetInt32(1000, 9999)}"),
            TransactionId = transactionId,
            BranchCode = branch,
            Amount = amount,
            Fee = CalculateFee(amount),
            Sender = command.Sender.Trim(),
            Recipient = command.Recipient.Trim(),
            Status = MoneyOrderStatus.Pending,
            CreatedAt = now,
        };

        return await moneyOrders.AddAsync(order, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IdentityVerificationEntity> RecordVerificationAsync(int transactionId, BranchCode branch, int staffId, IdentityVerificationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ReflectionValidator.EnsureValid(command);

        var verification = new IdentityVerificationEntity
        {
            TransactionId = transactionId,
            BranchCode = branch,
            StaffId = staffId,
            CustomerName = command.CustomerName.Trim(),
            DocumentType = command.DocumentType,
            // The raw number goes no further than this line.
            MaskedDocumentNumber = MaskedDocumentNumber.FromRaw(command.DocumentNumber),
            Result = command.Result,
            VerifiedAt = clock.GetUtcNow(),
        };

        return await verifications.AddAsync(verification, cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<IdentityVerificationEntity>> GetVerificationsForTransactionAsync(int transactionId, CancellationToken cancellationToken) =>
        verifications.FindAsync(Specification<IdentityVerificationEntity>.Where(v => v.TransactionId == transactionId), cancellationToken).AsTask();

    public Task<IReadOnlyList<MoneyOrderEntity>> GetForTransactionAsync(int transactionId, CancellationToken cancellationToken) =>
        moneyOrders.FindAsync(Specification<MoneyOrderEntity>.Where(m => m.TransactionId == transactionId), cancellationToken).AsTask();

    public async Task IssueAsync(int transactionId, int? identityVerificationId, CancellationToken cancellationToken)
    {
        var orders = await GetForTransactionAsync(transactionId, cancellationToken).ConfigureAwait(false);
        if (orders.Count == 0)
        {
            return;
        }

        var now = clock.GetUtcNow();
        foreach (var order in orders)
        {
            order.Status = MoneyOrderStatus.Issued;
            order.IssuedAt = now;
            if (rules.RequiresIdentityVerification(order.Amount))
            {
                order.IdentityVerificationId = identityVerificationId;
            }
        }

        await moneyOrders.UpdateRangeAsync(orders.ToList(), cancellationToken).ConfigureAwait(false);

        foreach (var order in orders)
        {
            events.Publish(new MoneyOrderIssuedEventArgs(order.Id, order.SerialNumber, order.BranchCode, order.Amount, rules.IsHighValueMoneyOrder(order.Amount), now));
        }
    }

    public async Task CancelAsync(int transactionId, CancellationToken cancellationToken)
    {
        var orders = await GetForTransactionAsync(transactionId, cancellationToken).ConfigureAwait(false);
        foreach (var order in orders)
        {
            order.Status = MoneyOrderStatus.Cancelled;
        }

        if (orders.Count > 0)
        {
            await moneyOrders.UpdateRangeAsync(orders.ToList(), cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task CancelByIdAsync(int moneyOrderId, CancellationToken cancellationToken)
    {
        var order = await moneyOrders.GetByIdAsync(moneyOrderId, cancellationToken).ConfigureAwait(false);
        if (order is { Status: MoneyOrderStatus.Pending })
        {
            order.Status = MoneyOrderStatus.Cancelled;
            await moneyOrders.UpdateAsync(order, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<MoneyOrderEntity>> GetForBranchAsync(BranchCode? branch, CancellationToken cancellationToken)
    {
        var spec = branch is { } code
            ? Specification<MoneyOrderEntity>.Where(m => m.BranchCode == code)
            : Specification<MoneyOrderEntity>.All;
        var list = await moneyOrders.FindAsync(spec, cancellationToken).ConfigureAwait(false);
        return list.OrderByDescending(m => m.CreatedAt).ToList();
    }
}
