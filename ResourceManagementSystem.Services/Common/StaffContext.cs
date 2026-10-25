using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Models.ValueObjects;

namespace ResourceManagementSystem.Services.Common;

/// <summary>
/// Who is performing an operation, taken from the signed-in user's claims by the controller.
/// Services never trust it blindly: they re-load the staff record to check it is still active
/// and still assigned to the branch (a cookie can outlive a deactivation).
/// </summary>
public sealed record StaffContext(int StaffId, StaffRole Role, BranchCode BranchCode, string Name)
{
    public bool IsAdministrator => Role == StaffRole.Administrator;
    public bool IsAuditor => Role == StaffRole.Auditor;
    public bool IsBranchManager => Role == StaffRole.BranchManager;

    /// <summary>Administrators and auditors may read every branch; everyone else only their own.</summary>
    public bool CanReadBranch(BranchCode branch) => IsAdministrator || IsAuditor || BranchCode == branch;
}
