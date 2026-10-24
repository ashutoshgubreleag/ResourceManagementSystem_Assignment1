using ResourceManagementSystem.Database.Models;

namespace ResourceManagementSystem.Services.Branch;

public interface IBranchService
{
    BranchEntity AddBranch(BranchEntity branch);
    BranchEntity? GetByCode(string branchCode);
    IEnumerable<BranchEntity> GetAll();
    bool IsBranchOpen(string branchCode);
    void UpdateStatus(string branchCode, BranchStatus status);
}
