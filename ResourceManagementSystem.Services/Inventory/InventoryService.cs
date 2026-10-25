using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Models.Exceptions;
using ResourceManagementSystem.Models.Validation;
using ResourceManagementSystem.Models.ValueObjects;
using ResourceManagementSystem.Repositories;
using ResourceManagementSystem.Repositories.Specifications;
using ResourceManagementSystem.Services.Caching;
using ResourceManagementSystem.Services.Common;
using ResourceManagementSystem.Services.Events;

namespace ResourceManagementSystem.Services.Inventory;

public sealed class InventoryService(
    IBaseEntityRepository<BranchStockEntity> stock,
    IReferenceDataCache referenceData,
    BranchLockProvider locks,
    BusinessRules rules,
    IApexPostEvents events,
    TimeProvider clock) : IInventoryService
{
    public Task<IReadOnlyList<BranchStockEntity>> GetStockForBranchAsync(BranchCode branch, CancellationToken cancellationToken) =>
        stock.FindAsync(ForBranch(branch), cancellationToken).AsTask();

    public async Task<int> GetAvailableQuantityAsync(BranchCode branch, Sku sku, CancellationToken cancellationToken)
    {
        var rows = await stock.FindAsync(ForBranch(branch).And(ForSku(sku)), cancellationToken).ConfigureAwait(false);
        return rows.Sum(r => r.QuantityOnHand);
    }

    public async Task DeductAsync(BranchCode branch, IReadOnlyCollection<StockQuantity> items, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            return;
        }

        // Read-check-write must be atomic per branch: two counters selling the last item.
        using var _ = await locks.AcquireAsync(branch, cancellationToken).ConfigureAwait(false);

        var rows = (await stock.FindAsync(ForBranch(branch), cancellationToken).ConfigureAwait(false))
            .ToDictionary(r => r.Sku);

        // Check every line first so a failure changes nothing.
        foreach (var group in items.GroupBy(i => i.Sku))
        {
            var wanted = group.Sum(i => i.Quantity);
            var onHand = rows.TryGetValue(group.Key, out var row) ? row.QuantityOnHand : 0;
            if (wanted > onHand)
            {
                throw new BusinessRuleViolationException(
                    "STOCK_INSUFFICIENT",
                    $"Insufficient stock for {group.Key} at {branch}: {onHand} on hand, {wanted} requested.");
            }
        }

        var changed = new List<BranchStockEntity>();
        var crossedIntoLow = new List<BranchStockEntity>();
        foreach (var group in items.GroupBy(i => i.Sku))
        {
            var row = rows[group.Key];
            var wasLow = rules.IsLowStock(row, rules.DefaultLowStockThreshold);
            row.QuantityOnHand -= group.Sum(i => i.Quantity);
            row.UpdatedAt = clock.GetUtcNow();
            changed.Add(row);
            if (!wasLow && rules.IsLowStock(row, rules.DefaultLowStockThreshold))
            {
                crossedIntoLow.Add(row);
            }
        }

        await stock.UpdateRangeAsync(changed, cancellationToken).ConfigureAwait(false);
        await PublishLowStockAsync(crossedIntoLow, cancellationToken).ConfigureAwait(false);
    }

    public async Task RestockAsync(BranchCode branch, IReadOnlyCollection<StockQuantity> items, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            return;
        }

        using var _ = await locks.AcquireAsync(branch, cancellationToken).ConfigureAwait(false);
        var rows = (await stock.FindAsync(ForBranch(branch), cancellationToken).ConfigureAwait(false)).ToDictionary(r => r.Sku);
        var changed = new List<BranchStockEntity>();
        foreach (var group in items.GroupBy(i => i.Sku))
        {
            if (rows.TryGetValue(group.Key, out var row))
            {
                row.QuantityOnHand += group.Sum(i => i.Quantity);
                row.UpdatedAt = clock.GetUtcNow();
                changed.Add(row);
            }
        }

        if (changed.Count > 0)
        {
            await stock.UpdateRangeAsync(changed, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<BranchStockEntity> AdjustAsync(StockAdjustmentCommand command, StaffContext actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(actor);
        ReflectionValidator.EnsureValid(command);

        var branch = new BranchCode(command.BranchCode);
        var sku = new Sku(command.Sku);
        if (!(actor.IsAdministrator || (actor.IsBranchManager && actor.BranchCode == branch)))
        {
            throw new ForbiddenOperationException("Only the branch manager or an administrator can adjust stock.");
        }

        var products = await referenceData.GetProductsAsync(cancellationToken).ConfigureAwait(false);
        if (!products.ContainsKey(sku))
        {
            throw new CommandValidationException(nameof(command.Sku), $"Unknown SKU {sku}.");
        }

        using var _ = await locks.AcquireAsync(branch, cancellationToken).ConfigureAwait(false);
        var row = (await stock.FindAsync(ForBranch(branch).And(ForSku(sku)), cancellationToken).ConfigureAwait(false)).FirstOrDefault();

        if (row is null)
        {
            row = new BranchStockEntity { BranchCode = branch, Sku = sku, ReorderThreshold = rules.DefaultLowStockThreshold };
            if (command.QuantityChange < 0)
            {
                throw new BusinessRuleViolationException("STOCK_INSUFFICIENT", $"There is no {sku} stock at {branch} to write off.");
            }

            row.QuantityOnHand = command.QuantityChange;
            row.UpdatedAt = clock.GetUtcNow();
            return await stock.AddAsync(row, cancellationToken).ConfigureAwait(false);
        }

        var wasLow = rules.IsLowStock(row, rules.DefaultLowStockThreshold);
        if (row.QuantityOnHand + command.QuantityChange < 0)
        {
            throw new BusinessRuleViolationException("STOCK_INSUFFICIENT", $"Cannot remove {-command.QuantityChange}; only {row.QuantityOnHand} of {sku} on hand.");
        }

        row.QuantityOnHand += command.QuantityChange;
        row.UpdatedAt = clock.GetUtcNow();
        await stock.UpdateAsync(row, cancellationToken).ConfigureAwait(false);

        if (!wasLow && rules.IsLowStock(row, rules.DefaultLowStockThreshold))
        {
            await PublishLowStockAsync([row], cancellationToken).ConfigureAwait(false);
        }

        return row;
    }

    private async Task PublishLowStockAsync(IReadOnlyCollection<BranchStockEntity> rows, CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return;
        }

        var products = await referenceData.GetProductsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var row in rows)
        {
            var name = products.TryGetValue(row.Sku, out var product) ? product.Name : row.Sku.Value;
            events.Publish(new StockLowEventArgs(row.BranchCode, row.Sku, name, row.QuantityOnHand, row.ReorderThreshold, clock.GetUtcNow()));
        }
    }

    private static Specification<BranchStockEntity> ForBranch(BranchCode branch) =>
        Specification<BranchStockEntity>.Where(s => s.BranchCode == branch, "branch");

    private static Specification<BranchStockEntity> ForSku(Sku sku) =>
        Specification<BranchStockEntity>.Where(s => s.Sku == sku, "sku");
}
