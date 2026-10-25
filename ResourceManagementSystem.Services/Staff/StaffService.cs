using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Repositories;

namespace ResourceManagementSystem.Services.Staff;

public class StaffService : IStaffService
{
    private readonly IBaseEntityRepository<StaffEntity> _staffRepository;

    public StaffService(IBaseEntityRepository<StaffEntity> staffRepository)
    {
        _staffRepository = staffRepository;
    }

    public StaffEntity AddStaff(StaffEntity staff)
    {
        ValidateStaff(staff);

        var exists = _staffRepository.GetAll().Any(existing =>
            string.Equals(existing.Email, staff.Email, StringComparison.OrdinalIgnoreCase));

        if (exists)
            throw new InvalidOperationException($"A staff member with email '{staff.Email}' already exists.");

        return _staffRepository.Add(staff);
    }

    public StaffEntity UpdateStaff(StaffEntity staff)
    {
        if (staff == null) throw new ArgumentNullException(nameof(staff));
        ValidateStaff(staff);

        var existing = GetById(staff.Id) ?? throw new InvalidOperationException("Staff member not found.");
        var duplicate = _staffRepository.GetAll()
            .Any(item => item.Id != staff.Id && string.Equals(item.Email, staff.Email, StringComparison.OrdinalIgnoreCase));

        if (duplicate)
            throw new InvalidOperationException($"A staff member with email '{staff.Email}' already exists.");

        existing.Name = staff.Name;
        existing.Email = staff.Email;
        existing.Role = staff.Role;
        existing.HomeBranchCode = staff.HomeBranchCode;
        existing.IsActive = staff.IsActive;

        var updated = _staffRepository.Update(existing);
        if (!updated) throw new InvalidOperationException("Unable to update staff member.");
        return existing;
    }

    public IEnumerable<StaffEntity> GetAll() => _staffRepository.GetAll();

    public StaffEntity? GetById(int id) => _staffRepository.GetById(id);

    public IEnumerable<StaffEntity> GetByBranch(string branchCode)
    {
        if (string.IsNullOrWhiteSpace(branchCode)) return Enumerable.Empty<StaffEntity>();
        return _staffRepository.GetAll()
            .Where(staff => string.Equals(staff.HomeBranchCode, branchCode, StringComparison.OrdinalIgnoreCase));
    }

    public void DeactivateStaff(int staffId)
    {
        var staff = GetById(staffId) ?? throw new InvalidOperationException("Staff member not found.");
        if (!staff.IsActive)
            return;

        staff.IsActive = false;
        var updated = _staffRepository.Update(staff);
        if (!updated) throw new InvalidOperationException("Unable to deactivate staff member.");
    }

    public bool CanProcessTransaction(int staffId, string branchCode)
    {
        var staff = GetById(staffId);
        if (staff == null || !staff.IsActive) return false;
        return string.Equals(staff.HomeBranchCode, branchCode, StringComparison.OrdinalIgnoreCase);
    }

    public bool CanVoidOrRefund(int staffId, string branchCode)
    {
        var staff = GetById(staffId);
        if (staff == null || !staff.IsActive) return false;
        if (!string.Equals(staff.HomeBranchCode, branchCode, StringComparison.OrdinalIgnoreCase)) return false;
        return staff.Role == StaffRole.BranchManager;
    }

    private static void ValidateStaff(StaffEntity staff)
    {
        if (staff == null) throw new ArgumentNullException(nameof(staff));
        if (string.IsNullOrWhiteSpace(staff.Name)) throw new InvalidOperationException("Staff name is required.");
        if (string.IsNullOrWhiteSpace(staff.Email)) throw new InvalidOperationException("Staff email is required.");
        if (string.IsNullOrWhiteSpace(staff.HomeBranchCode)) throw new InvalidOperationException("Home branch is required.");

        var validRoles = Enum.GetValues<StaffRole>();
        if (!validRoles.Contains(staff.Role))
            throw new InvalidOperationException("Invalid staff role selected.");
    }
}
