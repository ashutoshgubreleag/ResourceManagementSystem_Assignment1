using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Models.Validation;
using ResourceManagementSystem.Models.ValueObjects;
using ResourceManagementSystem.Services.Common;

namespace ResourceManagementSystem.Services.Branch;

public sealed record SaveBranchCommand
{
    [NotBlank, Matches("^BR-[0-9]{3}$", "in the form BR-000")]
    public string BranchCode { get; init; } = string.Empty;

    [NotBlank, MaxLengthRule(80)]
    public string Name { get; init; } = string.Empty;

    [NotBlank, MaxLengthRule(40)]
    public string Region { get; init; } = string.Empty;

    [NotBlank, MaxLengthRule(120)]
    public string OpeningHours { get; init; } = string.Empty;

    [InRange(1, 20)]
    public int CounterCount { get; init; } = 2;
}

public interface IBranchService
{
    Task<IReadOnlyList<BranchEntity>> GetAllAsync(CancellationToken cancellationToken);

    Task<BranchEntity> GetByCodeAsync(BranchCode branchCode, CancellationToken cancellationToken);

    Task<BranchEntity> CreateAsync(SaveBranchCommand command, StaffContext actor, CancellationToken cancellationToken);

    Task<BranchEntity> UpdateAsync(SaveBranchCommand command, StaffContext actor, CancellationToken cancellationToken);

    Task SetStatusAsync(BranchCode branchCode, BranchStatus status, StaffContext actor, CancellationToken cancellationToken);

    /// <summary>Throws if the branch is closed ("a closed branch cannot accept transactions").</summary>
    Task<BranchEntity> EnsureOpenForTradingAsync(BranchCode branchCode, CancellationToken cancellationToken);
}
