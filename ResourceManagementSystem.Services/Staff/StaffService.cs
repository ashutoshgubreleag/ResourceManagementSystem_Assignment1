using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Models.Exceptions;
using ResourceManagementSystem.Models.Validation;
using ResourceManagementSystem.Models.ValueObjects;
using ResourceManagementSystem.Repositories;
using ResourceManagementSystem.Repositories.Specifications;
using ResourceManagementSystem.Services.Authentication;
using ResourceManagementSystem.Services.Common;

namespace ResourceManagementSystem.Services.Staff;

public sealed class StaffService(
    IBaseEntityRepository<StaffEntity> staff,
    IBaseEntityRepository<BranchEntity> branches,
    IPasswordHasher hasher) : IStaffService
{
    private const int MinimumPasswordLength = 10;

    public async Task<IReadOnlyList<StaffEntity>> GetAllAsync(StaffContext actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var spec = actor.IsAdministrator || actor.IsAuditor
            ? Specification<StaffEntity>.All
            : Specification<StaffEntity>.Where(s => s.HomeBranchCode == actor.BranchCode, "own branch");

        var list = await staff.FindAsync(spec, cancellationToken).ConfigureAwait(false);
        return list.OrderBy(s => s.HomeBranchCode.Value, StringComparer.Ordinal).ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<StaffEntity> GetByIdAsync(int staffId, CancellationToken cancellationToken) =>
        await staff.GetByIdAsync(staffId, cancellationToken).ConfigureAwait(false)
        ?? throw new EntityNotFoundException("Staff member", staffId);

    public async Task<StaffEntity> CreateAsync(SaveStaffCommand command, StaffContext actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ReflectionValidator.EnsureValid(command);
        var branch = new BranchCode(command.HomeBranchCode);
        EnsureCanManage(actor, branch, command.Role);
        await EnsureBranchExistsAsync(branch, cancellationToken).ConfigureAwait(false);
        await EnsureEmailUniqueAsync(command.Email, 0, cancellationToken).ConfigureAwait(false);

        if (command.InitialPassword is not { Length: >= MinimumPasswordLength })
        {
            throw new CommandValidationException(nameof(command.InitialPassword), $"Initial password must be at least {MinimumPasswordLength} characters.");
        }

        var entity = new StaffEntity
        {
            Name = command.Name,
            Email = command.Email,
            Role = command.Role,
            HomeBranchCode = branch,
            IsActive = command.IsActive,
            PasswordHash = hasher.Hash(command.InitialPassword),
        };

        return await staff.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task<StaffEntity> UpdateAsync(SaveStaffCommand command, StaffContext actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ReflectionValidator.EnsureValid(command);

        var existing = await GetByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        var branch = new BranchCode(command.HomeBranchCode);
        EnsureCanManage(actor, existing.HomeBranchCode, existing.Role);
        EnsureCanManage(actor, branch, command.Role);
        await EnsureBranchExistsAsync(branch, cancellationToken).ConfigureAwait(false);
        await EnsureEmailUniqueAsync(command.Email, command.Id, cancellationToken).ConfigureAwait(false);

        if (existing.Id == actor.StaffId && !command.IsActive)
        {
            throw new BusinessRuleViolationException("SELF_DEACTIVATION", "You cannot deactivate your own account.");
        }

        existing.Name = command.Name;
        existing.Email = command.Email;
        existing.Role = command.Role;
        existing.HomeBranchCode = branch;
        existing.IsActive = command.IsActive;
        if (command.InitialPassword is { Length: > 0 } password)
        {
            existing.PasswordHash = password.Length >= MinimumPasswordLength
                ? hasher.Hash(password)
                : throw new CommandValidationException(nameof(command.InitialPassword), $"Password must be at least {MinimumPasswordLength} characters.");
        }

        await staff.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
        return existing;
    }

    public async Task DeactivateAsync(int staffId, StaffContext actor, CancellationToken cancellationToken)
    {
        var existing = await GetByIdAsync(staffId, cancellationToken).ConfigureAwait(false);
        EnsureCanManage(actor, existing.HomeBranchCode, existing.Role);
        if (existing.Id == actor.StaffId)
        {
            throw new BusinessRuleViolationException("SELF_DEACTIVATION", "You cannot deactivate your own account.");
        }

        if (!existing.IsActive)
        {
            return;
        }

        existing.IsActive = false;
        await staff.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
    }

    public async Task<StaffEntity> EnsureCanTransactAsync(StaffContext actor, BranchCode branch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var member = await staff.GetByIdAsync(actor.StaffId, cancellationToken).ConfigureAwait(false);

        return member switch
        {
            null => throw new ForbiddenOperationException("Your staff record no longer exists."),
            { IsActive: false } => throw new BusinessRuleViolationException("STAFF_INACTIVE", "Inactive staff cannot open or process transactions."),
            { Role: not (StaffRole.CounterClerk or StaffRole.BranchManager) } =>
                throw new ForbiddenOperationException("Only counter clerks and branch managers can process counter transactions."),
            _ when member.HomeBranchCode != branch =>
                throw new BusinessRuleViolationException("WRONG_BRANCH", $"You are assigned to {member.HomeBranchCode} and cannot transact at {branch}."),
            _ => member,
        };
    }

    /// <summary>Administrators manage everyone; branch managers manage clerks at their own branch.</summary>
    private static void EnsureCanManage(StaffContext actor, BranchCode branch, StaffRole targetRole)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var allowed = actor.Role switch
        {
            StaffRole.Administrator => true,
            StaffRole.BranchManager => actor.BranchCode == branch && targetRole == StaffRole.CounterClerk,
            _ => false,
        };

        if (!allowed)
        {
            throw new ForbiddenOperationException("You are not allowed to manage this staff member.");
        }
    }

    private async Task EnsureBranchExistsAsync(BranchCode branch, CancellationToken cancellationToken)
    {
        var found = await branches.FindAsync(Specification<BranchEntity>.Where(b => b.BranchCode == branch), cancellationToken).ConfigureAwait(false);
        if (found.Count == 0)
        {
            throw new CommandValidationException(nameof(SaveStaffCommand.HomeBranchCode), $"Branch {branch} does not exist.");
        }
    }

    private async Task EnsureEmailUniqueAsync(string email, int ignoreId, CancellationToken cancellationToken)
    {
        var normalised = email.Trim().ToLowerInvariant();
        var clash = await staff.FindAsync(Specification<StaffEntity>.Where(s => s.Email == normalised && s.Id != ignoreId), cancellationToken).ConfigureAwait(false);
        if (clash.Count > 0)
        {
            throw new CommandValidationException(nameof(SaveStaffCommand.Email), $"A staff member with email '{normalised}' already exists.");
        }
    }
}
