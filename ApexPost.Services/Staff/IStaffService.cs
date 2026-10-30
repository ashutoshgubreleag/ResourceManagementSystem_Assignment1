using ApexPost.Database.Models;
using ApexPost.Models.Validation;
using ApexPost.Models.ValueObjects;
using ApexPost.Services.Common;

namespace ApexPost.Services.Staff;

public sealed record SaveStaffCommand
{
    public int Id { get; init; }

    [NotBlank, MaxLengthRule(80)]
    public string Name { get; init; } = string.Empty;

    [NotBlank, MaxLengthRule(120), Matches("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", "a valid email address")]
    public string Email { get; init; } = string.Empty;

    [DefinedEnum]
    public StaffRole Role { get; init; }

    [NotBlank, Matches("^BR-[0-9]{3}$", "in the form BR-000")]
    public string HomeBranchCode { get; init; } = string.Empty;

    public bool IsActive { get; init; } = true;

    /// <summary>Initial password for new staff (ignored on update when empty). Never logged or audited.</summary>
    [MaxLengthRule(128)]
    public string? InitialPassword { get; init; }
}

public interface IStaffService
{
    Task<IReadOnlyList<StaffEntity>> GetAllAsync(StaffContext actor, CancellationToken cancellationToken);

    Task<StaffEntity> GetByIdAsync(int staffId, CancellationToken cancellationToken);

    Task<StaffEntity> CreateAsync(SaveStaffCommand command, StaffContext actor, CancellationToken cancellationToken);

    Task<StaffEntity> UpdateAsync(SaveStaffCommand command, StaffContext actor, CancellationToken cancellationToken);

    Task DeactivateAsync(int staffId, StaffContext actor, CancellationToken cancellationToken);

    /// <summary>
    /// Re-loads the actor and enforces "inactive staff cannot open or process transactions" and
    /// "a clerk can only transact at their assigned branch".
    /// </summary>
    Task<StaffEntity> EnsureCanTransactAsync(StaffContext actor, BranchCode branch, CancellationToken cancellationToken);
}
