using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Models.Exceptions;
using ResourceManagementSystem.Models.Validation;
using ResourceManagementSystem.Models.ValueObjects;
using ResourceManagementSystem.Repositories;
using ResourceManagementSystem.Repositories.Specifications;
using ResourceManagementSystem.Services.Branch;
using ResourceManagementSystem.Services.Catalog;
using ResourceManagementSystem.Services.Common;
using ResourceManagementSystem.Services.Events;
using ResourceManagementSystem.Services.Inventory;
using ResourceManagementSystem.Services.MoneyOrders;
using ResourceManagementSystem.Services.Shipping;
using ResourceManagementSystem.Services.Staff;

namespace ResourceManagementSystem.Services.Transactions;

/// <summary>
/// Orchestrates a counter transaction. Every mutating call re-checks the business rules that
/// can change between requests: staff still active and at this branch, branch still open,
/// transaction still open. Controllers stay thin; all rules live here or in the services it calls.
/// </summary>
public sealed class TransactionService(
    IBaseEntityRepository<TransactionEntity> transactions,
    IBaseEntityRepository<ShippingOrderEntity> shipments,
    IBranchService branches,
    IStaffService staff,
    IProductCatalogService catalog,
    IInventoryService inventory,
    IShippingService shipping,
    IMoneyOrderService moneyOrders,
    BusinessRules rules,
    IApexPostEvents events,
    TimeProvider clock) : ITransactionService
{
    public async Task<IReadOnlyList<TransactionEntity>> GetRecentAsync(StaffContext actor, BranchCode? branch, int take, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var scope = branch ?? (actor.IsAdministrator || actor.IsAuditor ? null : actor.BranchCode);
        if (scope is { } code && !actor.CanReadBranch(code))
        {
            throw new ForbiddenOperationException("You can only view transactions from your own branch.");
        }

        var spec = scope is { } c ? TransactionSpecifications.AtBranch(c) : Specification<TransactionEntity>.All;
        var list = await transactions.FindAsync(spec, cancellationToken).ConfigureAwait(false);
        return list.OrderByDescending(t => t.CreatedAt).Take(take).ToList();
    }

    public async Task<TransactionDetails> GetDetailsAsync(int transactionId, StaffContext actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var transaction = await GetAsync(transactionId, cancellationToken).ConfigureAwait(false);
        if (!actor.CanReadBranch(transaction.BranchCode))
        {
            throw new ForbiddenOperationException("You can only view transactions from your own branch.");
        }

        // Three independent reads for one screen: run them concurrently.
        var shipmentsTask = shipments.FindAsync(Specification<ShippingOrderEntity>.Where(s => s.TransactionId == transactionId), cancellationToken).AsTask();
        var moneyOrdersTask = moneyOrders.GetForTransactionAsync(transactionId, cancellationToken);
        var verificationsTask = moneyOrders.GetVerificationsForTransactionAsync(transactionId, cancellationToken);
        await Task.WhenAll(shipmentsTask, moneyOrdersTask, verificationsTask).ConfigureAwait(false);

        var verificationsList = await verificationsTask.ConfigureAwait(false);
        var required = RequiresIdentityCheck(transaction);
        var satisfied = FindPassedVerification(transaction, verificationsList) is not null;
        return new TransactionDetails(transaction, await shipmentsTask.ConfigureAwait(false), await moneyOrdersTask.ConfigureAwait(false), verificationsList, required, satisfied);
    }

    public async Task<TransactionEntity> OpenAsync(StaffContext actor, int counterTerminalId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var clerk = await staff.EnsureCanTransactAsync(actor, actor.BranchCode, cancellationToken).ConfigureAwait(false);
        var branch = await branches.EnsureOpenForTradingAsync(clerk.HomeBranchCode, cancellationToken).ConfigureAwait(false);

        if (!branch.Counters.Any(c => c.Id == counterTerminalId && c.IsActive))
        {
            throw new BusinessRuleViolationException("COUNTER_INVALID", $"Counter {counterTerminalId} is not an active terminal at {branch.BranchCode}.");
        }

        var transaction = new TransactionEntity
        {
            BranchCode = branch.BranchCode,
            CounterTerminalId = counterTerminalId,
            ClerkStaffId = clerk.Id,
            Status = TransactionStatus.Open,
            CreatedAt = clock.GetUtcNow(),
        };

        return await transactions.AddAsync(transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TransactionEntity> AddRetailItemAsync(int transactionId, AddRetailItemCommand command, StaffContext actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ReflectionValidator.EnsureValid(command);
        var transaction = await LoadOpenForUpdateAsync(transactionId, actor, cancellationToken).ConfigureAwait(false);

        var sku = new Sku(command.Sku);
        var product = await catalog.FindProductAsync(sku, cancellationToken).ConfigureAwait(false)
            ?? throw new CommandValidationException(nameof(command.Sku), $"Product {sku} is not in the catalogue.");

        // Early, friendly check; the authoritative check happens atomically at payment.
        var existing = transaction.LineItems.FirstOrDefault(l => l.LineType == TransactionLineType.RetailItem && l.ItemCode == sku.Value);
        var wanted = (existing?.Quantity ?? 0) + command.Quantity;
        var available = await inventory.GetAvailableQuantityAsync(transaction.BranchCode, sku, cancellationToken).ConfigureAwait(false);
        if (wanted > available)
        {
            throw new BusinessRuleViolationException("STOCK_INSUFFICIENT", $"Only {available} x {product.Name} in stock at {transaction.BranchCode}.");
        }

        var line = existing ?? new TransactionLineItemEntity
        {
            Id = transaction.NextLineNumber,
            LineType = TransactionLineType.RetailItem,
            ItemCode = sku.Value,
            Description = product.Name,
            UnitPrice = product.UnitPrice,
            TaxCategory = TaxRules.CategoryFor(TransactionLineType.RetailItem, product.TaxCategory),
        };

        line.Quantity = wanted;
        line.LineTotal = (line.UnitPrice * line.Quantity).RoundedToCents;
        line.TaxAmount = TaxRules.TaxOn(line.LineTotal, line.TaxCategory);
        if (existing is null)
        {
            transaction.LineItems.Add(line);
        }

        return await SaveAsync(transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ShippingOrderEntity> AddShipmentAsync(int transactionId, AddShipmentCommand command, StaffContext actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ReflectionValidator.EnsureValid(command.Recipient);
        var transaction = await LoadOpenForUpdateAsync(transactionId, actor, cancellationToken).ConfigureAwait(false);

        var quote = await shipping.QuoteAsync(command.Quote, cancellationToken).ConfigureAwait(false);
        var shipment = await shipping.CreateShipmentAsync(transaction.Id, transaction.BranchCode, quote, command.Recipient.ToAddress(), cancellationToken).ConfigureAwait(false);

        transaction.LineItems.Add(new TransactionLineItemEntity
        {
            Id = transaction.NextLineNumber,
            LineType = TransactionLineType.Shipping,
            ItemCode = shipment.TrackingNumber,
            Description = $"{shipment.ShippingType} {shipment.ServiceLevel} to {quote.Context.Zone.Name} ({quote.ChargeableWeight})",
            Quantity = 1,
            UnitPrice = quote.Net,
            LineTotal = quote.Net,
            TaxCategory = quote.TaxCategory,
            TaxAmount = quote.Tax,
            ReferenceId = shipment.Id,
        });

        await SaveAsync(transaction, cancellationToken).ConfigureAwait(false);
        return shipment;
    }

    public async Task<MoneyOrderEntity> AddMoneyOrderAsync(int transactionId, MoneyOrderCommand command, StaffContext actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var transaction = await LoadOpenForUpdateAsync(transactionId, actor, cancellationToken).ConfigureAwait(false);
        var order = await moneyOrders.CreatePendingAsync(transaction.Id, transaction.BranchCode, command, cancellationToken).ConfigureAwait(false);

        var taxCategory = TaxRules.CategoryFor(TransactionLineType.MoneyOrder);
        transaction.LineItems.Add(new TransactionLineItemEntity
        {
            Id = transaction.NextLineNumber,
            LineType = TransactionLineType.MoneyOrder,
            ItemCode = order.SerialNumber,
            Description = $"Money order {order.Amount} to {order.Recipient}",
            Quantity = 1,
            UnitPrice = order.Fee,
            LineTotal = order.Fee,
            TaxCategory = taxCategory,
            TaxAmount = TaxRules.TaxOn(order.Fee, taxCategory),
            PassThroughAmount = order.Amount,
            ReferenceId = order.Id,
        });

        await SaveAsync(transaction, cancellationToken).ConfigureAwait(false);
        return order;
    }

    public async Task<IdentityVerificationEntity> AddIdentityVerificationAsync(int transactionId, IdentityVerificationCommand command, StaffContext actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(actor);
        var transaction = await LoadOpenForUpdateAsync(transactionId, actor, cancellationToken).ConfigureAwait(false);
        var verification = await moneyOrders.RecordVerificationAsync(transaction.Id, transaction.BranchCode, actor.StaffId, command, cancellationToken).ConfigureAwait(false);

        var fee = rules.IdentityVerificationFee;
        var taxCategory = TaxRules.CategoryFor(TransactionLineType.IdentityVerification);
        transaction.LineItems.Add(new TransactionLineItemEntity
        {
            Id = transaction.NextLineNumber,
            LineType = TransactionLineType.IdentityVerification,
            ItemCode = "IDV",
            Description = $"Identity check: {verification.DocumentType} {verification.MaskedDocumentNumber} ({verification.Result})",
            Quantity = 1,
            UnitPrice = fee,
            LineTotal = fee,
            TaxCategory = taxCategory,
            TaxAmount = TaxRules.TaxOn(fee, taxCategory),
            ReferenceId = verification.Id,
        });

        await SaveAsync(transaction, cancellationToken).ConfigureAwait(false);
        return verification;
    }

    public async Task<TransactionEntity> RemoveLineAsync(int transactionId, int lineNumber, StaffContext actor, CancellationToken cancellationToken)
    {
        var transaction = await LoadOpenForUpdateAsync(transactionId, actor, cancellationToken).ConfigureAwait(false);
        var line = transaction.LineItems.FirstOrDefault(l => l.Id == lineNumber)
            ?? throw new EntityNotFoundException("Transaction line", lineNumber);

        switch (line)
        {
            case { LineType: TransactionLineType.Shipping, ReferenceId: int shipmentId }:
                await shipping.SetStatusAsync([shipmentId], ShipmentStatus.Cancelled, cancellationToken).ConfigureAwait(false);
                break;
            case { LineType: TransactionLineType.MoneyOrder, ReferenceId: int moneyOrderId }:
                await moneyOrders.CancelByIdAsync(moneyOrderId, cancellationToken).ConfigureAwait(false);
                break;
        }

        transaction.LineItems.Remove(line);
        return await SaveAsync(transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TransactionEntity> PayAsync(int transactionId, PaymentMethod paymentMethod, StaffContext actor, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(paymentMethod))
        {
            throw new CommandValidationException(nameof(paymentMethod), "Choose a payment method.");
        }

        var transaction = await LoadOpenForUpdateAsync(transactionId, actor, cancellationToken).ConfigureAwait(false);
        if (transaction.LineItems.Count == 0)
        {
            throw new BusinessRuleViolationException("BASKET_EMPTY", "Add at least one item or service before taking payment.");
        }

        // Rule: a money order above the threshold needs a passed identity verification in THIS transaction.
        IdentityVerificationEntity? passedVerification = null;
        if (RequiresIdentityCheck(transaction))
        {
            var verifications = await moneyOrders.GetVerificationsForTransactionAsync(transaction.Id, cancellationToken).ConfigureAwait(false);
            passedVerification = FindPassedVerification(transaction, verifications)
                ?? throw new BusinessRuleViolationException(
                    "MONEY_ORDER_ID_REQUIRED",
                    "This transaction contains a money order above the identity threshold. Record a passed identity verification before taking payment.");
        }

        // Rule: stock can never go negative. All-or-nothing, under the branch lock.
        var retail = transaction.LineItems
            .Where(l => l.LineType == TransactionLineType.RetailItem)
            .Select(l => new StockQuantity(new Sku(l.ItemCode), l.Quantity))
            .ToList();
        await inventory.DeductAsync(transaction.BranchCode, retail, cancellationToken).ConfigureAwait(false);

        await shipping.SetStatusAsync(ShipmentIds(transaction), ShipmentStatus.Lodged, cancellationToken).ConfigureAwait(false);
        await moneyOrders.IssueAsync(transaction.Id, passedVerification?.Id, cancellationToken).ConfigureAwait(false);

        transaction.PaymentMethod = paymentMethod;
        transaction.Status = TransactionStatus.Paid;
        transaction.ClosedAt = clock.GetUtcNow();
        await SaveAsync(transaction, cancellationToken).ConfigureAwait(false);

        events.Publish(new TransactionCompletedEventArgs(transaction.Id, transaction.BranchCode, transaction.TotalAmount, transaction.ClerkStaffId, transaction.ClosedAt.Value));
        return transaction;
    }

    public Task<TransactionEntity> VoidAsync(int transactionId, VoidOrRefundCommand command, StaffContext actor, CancellationToken cancellationToken) =>
        ReverseAsync(transactionId, command, actor, isRefund: false, cancellationToken);

    public Task<TransactionEntity> RefundAsync(int transactionId, VoidOrRefundCommand command, StaffContext actor, CancellationToken cancellationToken) =>
        ReverseAsync(transactionId, command, actor, isRefund: true, cancellationToken);

    private async Task<TransactionEntity> ReverseAsync(int transactionId, VoidOrRefundCommand command, StaffContext actor, bool isRefund, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(actor);
        var action = isRefund ? "refund" : "void";

        // Rule: voids and refunds require the Branch Manager role and a reason.
        if (actor.Role != StaffRole.BranchManager)
        {
            throw new ForbiddenOperationException($"Only a branch manager can {action} a transaction.");
        }

        ReflectionValidator.EnsureValid(command);
        var transaction = await GetAsync(transactionId, cancellationToken).ConfigureAwait(false);
        var manager = await staff.EnsureCanTransactAsync(actor, transaction.BranchCode, cancellationToken).ConfigureAwait(false);

        var allowed = (isRefund, transaction.Status) switch
        {
            (false, TransactionStatus.Open or TransactionStatus.Paid) => true,
            (true, TransactionStatus.Paid) => true,
            _ => false,
        };
        if (!allowed)
        {
            throw new BusinessRuleViolationException("INVALID_STATUS", $"A {transaction.Status} transaction cannot be {(isRefund ? "refunded" : "voided")}.");
        }

        if (transaction.Status == TransactionStatus.Paid)
        {
            var retail = transaction.LineItems
                .Where(l => l.LineType == TransactionLineType.RetailItem)
                .Select(l => new StockQuantity(new Sku(l.ItemCode), l.Quantity))
                .ToList();
            await inventory.RestockAsync(transaction.BranchCode, retail, cancellationToken).ConfigureAwait(false);
        }

        await shipping.SetStatusAsync(ShipmentIds(transaction), ShipmentStatus.Cancelled, cancellationToken).ConfigureAwait(false);
        await moneyOrders.CancelAsync(transaction.Id, cancellationToken).ConfigureAwait(false);

        transaction.Status = isRefund ? TransactionStatus.Refunded : TransactionStatus.Voided;
        transaction.VoidReason = command.Reason.Trim();
        transaction.AuthorisedByStaffId = manager.Id;
        transaction.ClosedAt ??= clock.GetUtcNow();
        await transactions.UpdateAsync(transaction, cancellationToken).ConfigureAwait(false);

        events.Publish(new TransactionVoidedEventArgs(transaction.Id, transaction.BranchCode, manager.Id, transaction.VoidReason, isRefund, transaction.TotalAmount, clock.GetUtcNow()));
        return transaction;
    }

    private async Task<TransactionEntity> GetAsync(int transactionId, CancellationToken cancellationToken) =>
        await transactions.GetByIdAsync(transactionId, cancellationToken).ConfigureAwait(false)
        ?? throw new EntityNotFoundException("Transaction", transactionId);

    private async Task<TransactionEntity> LoadOpenForUpdateAsync(int transactionId, StaffContext actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var transaction = await GetAsync(transactionId, cancellationToken).ConfigureAwait(false);

        await staff.EnsureCanTransactAsync(actor, transaction.BranchCode, cancellationToken).ConfigureAwait(false);
        await branches.EnsureOpenForTradingAsync(transaction.BranchCode, cancellationToken).ConfigureAwait(false);

        return transaction.IsOpen
            ? transaction
            : throw new BusinessRuleViolationException("TRANSACTION_NOT_OPEN", $"Transaction {transactionId} is {transaction.Status} and can no longer be changed.");
    }

    private async Task<TransactionEntity> SaveAsync(TransactionEntity transaction, CancellationToken cancellationToken)
    {
        transaction.RecalculateTotals();
        await transactions.UpdateAsync(transaction, cancellationToken).ConfigureAwait(false);
        return transaction;
    }

    private bool RequiresIdentityCheck(TransactionEntity transaction) =>
        transaction.LineItems.Any(l => l.LineType == TransactionLineType.MoneyOrder && rules.RequiresIdentityVerification(l.PassThroughAmount));

    private static IdentityVerificationEntity? FindPassedVerification(TransactionEntity transaction, IReadOnlyList<IdentityVerificationEntity> verifications)
    {
        // Only verifications still on the basket count (a removed line no longer applies).
        var onBasket = transaction.LineItems
            .Where(l => l.LineType == TransactionLineType.IdentityVerification && l.ReferenceId is not null)
            .Select(l => l.ReferenceId!.Value)
            .ToHashSet();
        return verifications.FirstOrDefault(v => v.Result == VerificationResult.Passed && onBasket.Contains(v.Id));
    }

    private static List<int> ShipmentIds(TransactionEntity transaction) =>
        transaction.LineItems
            .Where(l => l is { LineType: TransactionLineType.Shipping, ReferenceId: not null })
            .Select(l => l.ReferenceId!.Value)
            .ToList();
}
