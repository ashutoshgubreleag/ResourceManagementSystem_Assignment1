using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Models.Exceptions;
using ResourceManagementSystem.Models.Validation;
using ResourceManagementSystem.Models.ValueObjects;
using ResourceManagementSystem.Repositories;
using ResourceManagementSystem.Repositories.Specifications;
using ResourceManagementSystem.Services.Common;

namespace ResourceManagementSystem.Services.Branch;

public sealed class BranchService(IBaseEntityRepository<BranchEntity> branches) : IBranchService
{
    public async Task<IReadOnlyList<BranchEntity>> GetAllAsync(CancellationToken cancellationToken)
    {
        var all = await branches.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return all.OrderBy(b => b.BranchCode.Value, StringComparer.Ordinal).ToList();
    }

    public async Task<BranchEntity> GetByCodeAsync(BranchCode branchCode, CancellationToken cancellationToken)
    {
        var matches = await branches.FindAsync(ByCode(branchCode), cancellationToken).ConfigureAwait(false);
        return matches.FirstOrDefault() ?? throw new EntityNotFoundException("Branch", branchCode);
    }

    public async Task<BranchEntity> CreateAsync(SaveBranchCommand command, StaffContext actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureAdministrator(actor);
        ReflectionValidator.EnsureValid(command);

        var code = new BranchCode(command.BranchCode);
        var existing = await branches.FindAsync(ByCode(code), cancellationToken).ConfigureAwait(false);
        if (existing.Count > 0)
        {
            throw new BusinessRuleViolationException("BRANCH_EXISTS", $"Branch {code} already exists.");
        }

        var branch = new BranchEntity
        {
            BranchCode = code,
            Name = command.Name,
            Region = command.Region,
            OpeningHours = command.OpeningHours,
            Status = BranchStatus.Active,
            Counters = Enumerable.Range(1, command.CounterCount)
                .Select(n => new CounterTerminalEntity { Id = n, Name = $"Counter {n}", IsActive = true })
                .ToList(),
        };

        return await branches.AddAsync(branch, cancellationToken).ConfigureAwait(false);
    }

    public async Task<BranchEntity> UpdateAsync(SaveBranchCommand command, StaffContext actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureAdministrator(actor);
        ReflectionValidator.EnsureValid(command);

        var branch = await GetByCodeAsync(new BranchCode(command.BranchCode), cancellationToken).ConfigureAwait(false);
        branch.Name = command.Name;
        branch.Region = command.Region;
        branch.OpeningHours = command.OpeningHours;

        // Grow or shrink the counter list without renumbering existing terminals.
        for (var n = branch.Counters.Count + 1; n <= command.CounterCount; n++)
        {
            branch.Counters.Add(new CounterTerminalEntity { Id = n, Name = $"Counter {n}", IsActive = true });
        }

        foreach (var counter in branch.Counters)
        {
            counter.IsActive = counter.Id <= command.CounterCount;
        }

        await branches.UpdateAsync(branch, cancellationToken).ConfigureAwait(false);
        return branch;
    }

    public async Task SetStatusAsync(BranchCode branchCode, BranchStatus status, StaffContext actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!actor.IsAdministrator && !(actor.IsBranchManager && actor.BranchCode == branchCode))
        {
            throw new ForbiddenOperationException("Only an administrator or the branch's manager can open or close a branch.");
        }

        var branch = await GetByCodeAsync(branchCode, cancellationToken).ConfigureAwait(false);
        branch.Status = status;
        await branches.UpdateAsync(branch, cancellationToken).ConfigureAwait(false);
    }

    public async Task<BranchEntity> EnsureOpenForTradingAsync(BranchCode branchCode, CancellationToken cancellationToken)
    {
        var branch = await GetByCodeAsync(branchCode, cancellationToken).ConfigureAwait(false);
        return branch.Status == BranchStatus.Active
            ? branch
            : throw new BusinessRuleViolationException("BRANCH_CLOSED", $"Branch {branchCode} is closed and cannot accept transactions.");
    }

    private static Specification<BranchEntity> ByCode(BranchCode code) =>
        Specification<BranchEntity>.Where(b => b.BranchCode == code, "branch code");

    private static void EnsureAdministrator(StaffContext actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!actor.IsAdministrator)
        {
            throw new ForbiddenOperationException("Only administrators can create or edit branches.");
        }
    }
}
