using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Repositories;

namespace ResourceManagementSystem.Services.Branch;

public class BranchService : IBranchService
{
    private readonly IBaseEntityRepository<BranchEntity> _branchRepository;

    public BranchService(IBaseEntityRepository<BranchEntity> branchRepository)
    {
        _branchRepository = branchRepository;
    }

    public BranchEntity AddBranch(BranchEntity branch)
    {
        if (branch == null) throw new ArgumentNullException(nameof(branch));
        if (string.IsNullOrWhiteSpace(branch.BranchCode)) throw new InvalidOperationException("Branch code is required.");
        if (string.IsNullOrWhiteSpace(branch.Name)) throw new InvalidOperationException("Branch name is required.");

        var exists = _branchRepository.GetAll().Any(existing =>
            string.Equals(existing.BranchCode, branch.BranchCode, StringComparison.OrdinalIgnoreCase));

        if (exists)
            throw new InvalidOperationException($"Branch '{branch.BranchCode}' already exists.");

        return _branchRepository.Add(branch);
    }

    public BranchEntity? GetByCode(string branchCode)
    {
        if (string.IsNullOrWhiteSpace(branchCode)) return null;

        return _branchRepository.GetAll()
            .FirstOrDefault(branch => string.Equals(branch.BranchCode, branchCode, StringComparison.OrdinalIgnoreCase));
    }

    public IEnumerable<BranchEntity> GetAll() => _branchRepository.GetAll();

    public bool IsBranchOpen(string branchCode)
    {
        var branch = GetByCode(branchCode);
        return branch != null && branch.Status == BranchStatus.Active;
    }

    public void UpdateStatus(string branchCode, BranchStatus status)
    {
        var branch = GetByCode(branchCode) ?? throw new InvalidOperationException("Branch not found.");
        branch.Status = status;
        var updated = _branchRepository.Update(branch);
        if (!updated) throw new InvalidOperationException("Branch status could not be updated.");
    }
}
