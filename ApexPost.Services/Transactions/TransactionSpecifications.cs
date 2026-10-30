using ApexPost.Database.Models;
using ApexPost.Models.ValueObjects;
using ApexPost.Repositories.Specifications;

namespace ApexPost.Services.Transactions;

/// <summary>Named, composable filters used by reports and the dashboard.</summary>
public static class TransactionSpecifications
{
    public static Specification<TransactionEntity> AtBranch(BranchCode branch) =>
        Specification<TransactionEntity>.Where(t => t.BranchCode == branch, $"branch {branch}");

    public static Specification<TransactionEntity> WithStatus(TransactionStatus status) =>
        Specification<TransactionEntity>.Where(t => t.Status == status, $"status {status}");

    /// <summary>Completed (paid) on the given UTC calendar day.</summary>
    public static Specification<TransactionEntity> ClosedOn(DateOnly day) =>
        Specification<TransactionEntity>.Where(t => t.ClosedAt is { } closed && DateOnly.FromDateTime(closed.UtcDateTime) == day, $"closed on {day:yyyy-MM-dd}");

    public static Specification<TransactionEntity> PaidOn(DateOnly day) => WithStatus(TransactionStatus.Paid) & ClosedOn(day);
}
