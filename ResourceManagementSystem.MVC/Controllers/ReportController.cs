using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResourceManagementSystem.Services.Branch;
using ResourceManagementSystem.Services.Reports;

namespace ResourceManagementSystem.MVC.Controllers;

[Authorize]
public class ReportController : Controller
{
    private readonly IReportService _reportService;
    private readonly IBranchService _branchService;

    public ReportController(
        IReportService reportService,
        IBranchService branchService)
    {
        _reportService = reportService;
        _branchService = branchService;
    }

    public IActionResult Index()
    {
        var branches = _branchService.GetAll().ToList();
        var model = branches.Select(branch => new
        {
            branch.BranchCode,
            branch.Name,
            LowStock = _reportService.GetLowStockReport(branch.BranchCode, 5).ToList(),
            Sales = _reportService.GetDailyBranchSales(branch.BranchCode, DateTime.UtcNow).ToList()
        }).ToList();

        return View(model);
    }
}
