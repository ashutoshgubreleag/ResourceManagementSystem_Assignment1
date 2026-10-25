using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.MVC.Security;
using ResourceManagementSystem.Utilities;
using ResourceManagementSystem.Utilities.Audit;

namespace ResourceManagementSystem.MVC.Controllers;

/// <summary>Audit log viewer for auditors and administrators. Lives under /admin, so it is IP-restricted.</summary>
[IpRestricted]
[Authorize(Roles = nameof(StaffRole.Administrator) + "," + nameof(StaffRole.Auditor))]
public sealed class AdminController(IAuditStore audit) : ApexPostController
{
    public async Task<IActionResult> Audit(int take = 200, CancellationToken cancellationToken = default) =>
        View(await audit.ReadRecentAsync(Math.Clamp(take, 1, 1000), cancellationToken));
}
