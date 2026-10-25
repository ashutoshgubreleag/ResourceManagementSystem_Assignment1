using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Models.Exceptions;
using ResourceManagementSystem.Models.ValueObjects;
using ResourceManagementSystem.Repositories;
using ResourceManagementSystem.Repositories.Specifications;
using ResourceManagementSystem.Services.Caching;
using ResourceManagementSystem.Services.Common;
using ResourceManagementSystem.Services.Transactions;

namespace ResourceManagementSystem.Services.Reports;

public sealed class ReportService(
    IBaseEntityRepository<TransactionEntity> transactions,
    IBaseEntityRepository<BranchEntity> branches,
    IBaseEntityRepository<BranchStockEntity> stock,
    IBaseEntityRepository<ShippingOrderEntity> shipments,
    IBaseEntityRepository<MoneyOrderEntity> moneyOrders,
    IBaseEntityRepository<IdentityVerificationEntity> verifications,
    IReferenceDataCache referenceData,
    BusinessRules rules,
    TimeProvider clock) : IReportService
{
    public async Task<DateOnly> GetLatestTradingDayAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var paid = await transactions.FindAsync(TransactionSpecifications.WithStatus(TransactionStatus.Paid), cancellationToken).ConfigureAwait(false);
        var latest = paid
            .Where(t => t.ClosedAt is not null)
            .Select(t => DateOnly.FromDateTime(t.ClosedAt!.Value.UtcDateTime))
            .Where(d => d <= today)
            .DefaultIfEmpty(today)
            .Max();
        return latest;
    }

    public async Task<DailySalesReport> GetDailySalesAsync(DateOnly date, BranchCode? branch, StaffContext actor, CancellationToken cancellationToken)
    {
        var scope = Scope(branch, actor);
        var dayClosed = TransactionSpecifications.ClosedOn(date);
        var spec = scope is { } code ? dayClosed & TransactionSpecifications.AtBranch(code) : dayClosed;

        var branchListTask = branches.GetAllAsync(cancellationToken).AsTask();
        var dayTask = transactions.FindAsync(spec, cancellationToken).AsTask();
        await Task.WhenAll(branchListTask, dayTask).ConfigureAwait(false);

        var day = await dayTask.ConfigureAwait(false);
        var rows = (await branchListTask.ConfigureAwait(false))
            .Where(b => scope is null || b.BranchCode == scope)
            .OrderBy(b => b.BranchCode.Value, StringComparer.Ordinal)
            .Select(b =>
            {
                var atBranch = day.Where(t => t.BranchCode == b.BranchCode).ToList();
                var paid = atBranch.Where(t => t.Status == TransactionStatus.Paid).ToList();
                return new DailySalesRow(
                    b.BranchCode,
                    b.Name,
                    paid.Count,
                    paid.Select(t => t.RetailNet).Total(),
                    paid.Select(t => t.ServicesNet).Total(),
                    paid.Select(t => t.TaxAmount).Total(),
                    paid.Select(t => t.PassThroughAmount).Total(),
                    paid.Select(t => t.TotalAmount).Total(),
                    atBranch.Count(t => t.Status == TransactionStatus.Voided),
                    atBranch.Count(t => t.Status == TransactionStatus.Refunded));
            })
            .ToList();

        return new DailySalesReport(date, rows);
    }

    public async Task<LowStockReport> GetLowStockAsync(BranchCode? branch, int? threshold, StaffContext actor, CancellationToken cancellationToken)
    {
        var scope = Scope(branch, actor);
        var limit = Math.Clamp(threshold ?? rules.DefaultLowStockThreshold, 0, 1000);

        // The low-stock rule is a delegate from configuration, wrapped as a specification.
        var isLow = Specification<BranchStockEntity>.Where(s => rules.IsLowStock(s, limit), "low stock");
        var spec = scope is { } code ? isLow & Specification<BranchStockEntity>.Where(s => s.BranchCode == code) : isLow;

        var rowsTask = stock.FindAsync(spec, cancellationToken).AsTask();
        var productsTask = referenceData.GetProductsAsync(cancellationToken).AsTask();
        await Task.WhenAll(rowsTask, productsTask).ConfigureAwait(false);

        var products = await productsTask.ConfigureAwait(false);
        var rows = (await rowsTask.ConfigureAwait(false))
            .Select(s => products.TryGetValue(s.Sku, out var p)
                ? new LowStockRow(s.BranchCode, s.Sku, p.Name, p.Category, s.QuantityOnHand, s.ReorderThreshold)
                : new LowStockRow(s.BranchCode, s.Sku, s.Sku.Value, ProductCategory.OfficeSupplies, s.QuantityOnHand, s.ReorderThreshold))
            .OrderBy(r => r.BranchCode.Value, StringComparer.Ordinal)
            .ThenBy(r => r.QuantityOnHand)
            .ToList();

        return new LowStockReport(limit, rows);
    }

    public async Task<ShippingVolumeReport> GetShippingVolumeAsync(DateOnly from, DateOnly to, BranchCode? branch, StaffContext actor, CancellationToken cancellationToken)
    {
        EnsureRange(from, to);
        var scope = Scope(branch, actor);
        var spec = Specification<ShippingOrderEntity>.Where(s => s.Status == ShipmentStatus.Lodged && InRange(s.CreatedAt, from, to), "lodged in range");
        if (scope is { } code)
        {
            spec &= Specification<ShippingOrderEntity>.Where(s => s.BranchCode == code);
        }

        var shipmentsTask = shipments.FindAsync(spec, cancellationToken).AsTask();
        var zonesTask = referenceData.GetZonesAsync(cancellationToken).AsTask();
        await Task.WhenAll(shipmentsTask, zonesTask).ConfigureAwait(false);

        var zones = await zonesTask.ConfigureAwait(false);
        var rows = (await shipmentsTask.ConfigureAwait(false))
            .GroupBy(s => (s.ZoneCode, s.ServiceLevel))
            .Select(g => new ShippingVolumeRow(
                g.Key.ZoneCode,
                zones.TryGetValue(g.Key.ZoneCode, out var z) ? z.Name : g.Key.ZoneCode,
                g.Key.ServiceLevel,
                g.Count(),
                new Weight(g.Sum(s => s.Weight.Kilograms)),
                g.Select(s => s.Price).Total()))
            .OrderBy(r => r.ZoneCode, StringComparer.Ordinal)
            .ThenBy(r => r.ServiceLevel)
            .ToList();

        return new ShippingVolumeReport(from, to, rows);
    }

    public async Task<MoneyOrderAuditReport> GetMoneyOrderAuditAsync(DateOnly from, DateOnly to, BranchCode? branch, StaffContext actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        EnsureRange(from, to);
        if (actor.Role == StaffRole.CounterClerk)
        {
            throw new ForbiddenOperationException("The money order audit view is for auditors, managers and administrators.");
        }

        var scope = Scope(branch, actor);
        var ordersTask = moneyOrders.FindAsync(
            Specification<MoneyOrderEntity>.Where(m => m.Status != MoneyOrderStatus.Pending && InRange(m.CreatedAt, from, to) && (scope == null || m.BranchCode == scope)),
            cancellationToken).AsTask();
        var verificationsTask = verifications.FindAsync(
            Specification<IdentityVerificationEntity>.Where(v => InRange(v.VerifiedAt, from, to) && (scope == null || v.BranchCode == scope)),
            cancellationToken).AsTask();
        var branchesTask = branches.GetAllAsync(cancellationToken).AsTask();
        await Task.WhenAll(ordersTask, verificationsTask, branchesTask).ConfigureAwait(false);

        var orders = await ordersTask.ConfigureAwait(false);
        var checks = await verificationsTask.ConfigureAwait(false);
        var rows = (await branchesTask.ConfigureAwait(false))
            .Where(b => scope is null || b.BranchCode == scope)
            .OrderBy(b => b.BranchCode.Value, StringComparer.Ordinal)
            .Select(b =>
            {
                var issued = orders.Where(o => o.BranchCode == b.BranchCode && o.Status == MoneyOrderStatus.Issued).ToList();
                var branchChecks = checks.Where(v => v.BranchCode == b.BranchCode).ToList();
                return new MoneyOrderAuditRow(
                    b.BranchCode,
                    b.Name,
                    issued.Count,
                    issued.Select(o => o.Amount).Total(),
                    issued.Select(o => o.Fee).Total(),
                    issued.Count(o => rules.IsHighValueMoneyOrder(o.Amount)),
                    branchChecks.Count(v => v.Result == VerificationResult.Passed),
                    branchChecks.Count(v => v.Result == VerificationResult.Failed));
            })
            .ToList();

        return new MoneyOrderAuditReport(
            from,
            to,
            rows,
            orders.OrderByDescending(o => o.CreatedAt).ToList(),
            checks.OrderByDescending(v => v.VerifiedAt).ToList());
    }

    /// <summary>Clerks and managers see their own branch; auditors and admins may pick any (or all).</summary>
    private static BranchCode? Scope(BranchCode? requested, StaffContext actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.IsAdministrator || actor.IsAuditor)
        {
            return requested;
        }

        if (requested is { } code && code != actor.BranchCode)
        {
            throw new ForbiddenOperationException("You can only report on your own branch.");
        }

        return actor.BranchCode;
    }

    private static void EnsureRange(DateOnly from, DateOnly to)
    {
        if (to < from || to.DayNumber - from.DayNumber > 366)
        {
            throw new CommandValidationException("to", "Choose a date range of up to one year with 'to' on or after 'from'.");
        }
    }

    private static bool InRange(DateTimeOffset at, DateOnly from, DateOnly to)
    {
        var day = DateOnly.FromDateTime(at.UtcDateTime);
        return day >= from && day <= to;
    }
}
