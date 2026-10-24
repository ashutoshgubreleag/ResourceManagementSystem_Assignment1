using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Services.Branch;

namespace ResourceManagementSystem.MVC.Controllers;

[Authorize]
public class BranchController : Controller
{
    private readonly IBranchService _branchService;

    public BranchController(IBranchService branchService)
    {
        _branchService = branchService;
    }

    public IActionResult Index()
    {
        var branches = _branchService.GetAll().ToList();
        return View(branches);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new BranchEntity());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(BranchEntity model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            _branchService.AddBranch(model);
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }
}
