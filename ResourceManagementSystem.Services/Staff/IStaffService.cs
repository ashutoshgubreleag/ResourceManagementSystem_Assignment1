using ResourceManagementSystem.Database.Models;

namespace ResourceManagementSystem.Services.Staff;

public interface IStaffService
{
    StaffEntity AddStaff(StaffEntity staff);
    IEnumerable<StaffEntity> GetAll();
    StaffEntity? GetById(int id);
    IEnumerable<StaffEntity> GetByBranch(string branchCode);
    void DeactivateStaff(int staffId);
    bool CanProcessTransaction(int staffId, string branchCode);
    bool CanVoidOrRefund(int staffId, string branchCode);
}
