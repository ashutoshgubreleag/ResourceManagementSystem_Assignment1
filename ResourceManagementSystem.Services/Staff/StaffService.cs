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
        if (staff == null) throw new ArgumentNullException(nameof(staff));
        if (string.IsNullOrWhiteSpace(staff.Name)) throw new InvalidOperationException("Staff name is required.");
        if (string.IsNullOrWhiteSpace(staff.Email)) throw new InvalidOperationException("Staff email is required.");
        if (string.IsNullOrWhiteSpace(staff.HomeBranchCode)) throw new InvalidOperationException("Home branch is required.");

        var exists = _staffRepository.GetAll().Any(existing =>
            string.Equals(existing.Email, staff.Email, StringComparison.OrdinalIgnoreCase));

        if (exists)
            throw new InvalidOperationException($"A staff member with email '{staff.Email}' already exists.");

        return _staffRepository.Add(staff);
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
}
