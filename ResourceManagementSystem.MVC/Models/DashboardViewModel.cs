using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Services.Dashboard;

namespace ResourceManagementSystem.MVC.Models;

public sealed record DashboardViewModel(BranchDashboard Dashboard, IReadOnlyList<BranchEntity> Branches, bool CanSwitchBranch, bool CanTransact);
