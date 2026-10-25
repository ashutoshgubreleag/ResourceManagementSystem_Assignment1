using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Services.Branch;
using ResourceManagementSystem.Services.Staff;

namespace ResourceManagementSystem.MVC.Controllers;

[Authorize]
public class StaffController : Controller
{
    private readonly IStaffService _staffService;
    private readonly IBranchService _branchService;

    public StaffController(IStaffService staffService, IBranchService branchService)
    {
        _staffService = staffService;
        _branchService = branchService;
    }

    public IActionResult Index()
    {
        var staff = _staffService.GetAll().OrderBy(s => s.Name).ToList();
        return View(staff);
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewBag.BranchOptions = BuildBranchSelectList();
        return View(new StaffEntity { IsActive = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(StaffEntity model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.BranchOptions = BuildBranchSelectList();
            return View(model);
        }

        try
        {
            _staffService.AddStaff(model);
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ViewBag.BranchOptions = BuildBranchSelectList();
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpGet]
    public IActionResult Edit(int id)
    {
        var staff = _staffService.GetById(id);
        if (staff is null)
        {
            return NotFound();
        }

        ViewBag.BranchOptions = BuildBranchSelectList();
        return View(staff);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(StaffEntity model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.BranchOptions = BuildBranchSelectList();
            return View(model);
        }

        try
        {
            _staffService.UpdateStaff(model);
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ViewBag.BranchOptions = BuildBranchSelectList();
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Deactivate(int id)
    {
        try
        {
            _staffService.DeactivateStaff(id);
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    private List<SelectListItem> BuildBranchSelectList()
    {
        return _branchService.GetAll()
            .OrderBy(b => b.BranchCode)
            .Select(b => new SelectListItem
            {
                Value = b.BranchCode,
                Text = $"{b.BranchCode} - {b.Name}"
            })
            .ToList();
    }
}
