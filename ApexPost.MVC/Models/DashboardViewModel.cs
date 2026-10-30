using ApexPost.Database.Models;
using ApexPost.Services.Dashboard;

namespace ApexPost.MVC.Models;

public sealed record DashboardViewModel(BranchDashboard Dashboard, IReadOnlyList<BranchEntity> Branches, bool CanSwitchBranch, bool CanTransact);
