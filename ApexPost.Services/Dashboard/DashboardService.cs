using System.Diagnostics;
using ApexPost.Database.Models;
using ApexPost.Models.Exceptions;
using ApexPost.Models.ValueObjects;
using ApexPost.Services.Branch;
using ApexPost.Services.Common;
using ApexPost.Services.Notifications;
using ApexPost.Services.Reports;
using ApexPost.Services.Transactions;

namespace ApexPost.Services.Dashboard;

public sealed record BranchDashboard(
    BranchEntity Branch,
    DailySalesRow Sales,
    DateOnly SalesDate,
    LowStockReport LowStock,
    IReadOnlyList<NotificationEntity> Alerts,
    IReadOnlyList<TransactionEntity> RecentTransactions,
    int OpenTransactionCount,
    long BuildMilliseconds);

public interface IDashboardService
{
    Task<BranchDashboard> BuildAsync(BranchCode branch, StaffContext actor, CancellationToken cancellationToken);
}

/// <summary>
/// The branch dashboard needs five independent reads. Awaited one after another the page costs
/// the sum of their latencies; with <see cref="Task.WhenAll(Task[])"/> it costs roughly the slowest
/// one. The work is I/O (file reads), so this adds no threads, it just overlaps the waiting.
/// </summary>
public sealed class DashboardService(
    IBranchService branches,
    IReportService reports,
    INotificationService notifications,
    ITransactionService transactions) : IDashboardService
{
    public async Task<BranchDashboard> BuildAsync(BranchCode branch, StaffContext actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!actor.CanReadBranch(branch))
        {
            throw new ForbiddenOperationException("You can only view your own branch dashboard.");
        }

        var stopwatch = Stopwatch.StartNew();
        var salesDate = await reports.GetLatestTradingDayAsync(cancellationToken).ConfigureAwait(false);

        var branchTask = branches.GetByCodeAsync(branch, cancellationToken);
        var salesTask = reports.GetDailySalesAsync(salesDate, branch, actor, cancellationToken);
        var stockTask = reports.GetLowStockAsync(branch, null, actor, cancellationToken);
        var alertsTask = notifications.GetRecentAsync(branch, 8, cancellationToken);
        var recentTask = transactions.GetRecentAsync(actor, branch, 50, cancellationToken);

        await Task.WhenAll(branchTask, salesTask, stockTask, alertsTask, recentTask).ConfigureAwait(false);
        stopwatch.Stop();

        var recent = await recentTask.ConfigureAwait(false);
        var sales = (await salesTask.ConfigureAwait(false)).Rows.FirstOrDefault()
            ?? new DailySalesRow(branch, branch.Value, 0, Money.Zero, Money.Zero, Money.Zero, Money.Zero, Money.Zero, 0, 0);

        return new BranchDashboard(
            await branchTask.ConfigureAwait(false),
            sales,
            salesDate,
            await stockTask.ConfigureAwait(false),
            await alertsTask.ConfigureAwait(false),
            recent.Take(8).ToList(),
            recent.Count(t => t.Status == TransactionStatus.Open),
            stopwatch.ElapsedMilliseconds);
    }
}
