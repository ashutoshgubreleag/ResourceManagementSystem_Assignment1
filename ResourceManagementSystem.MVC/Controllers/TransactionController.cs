using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Services.Transactions;
using System.Security.Claims;

namespace ResourceManagementSystem.MVC.Controllers;

[Authorize]
public class TransactionController : Controller
{
    private readonly ITransactionService _transactionService;

    public TransactionController(ITransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    public IActionResult Index()
    {
        return View(_transactionService.GetAllTransactions().OrderByDescending(x => x.CreatedAt).ToList());
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new TransactionEntity());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(TransactionEntity model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        model.CreatedAt = DateTime.UtcNow;
        model.Status = TransactionStatus.Open;
        _transactionService.CreateTransaction(model);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "BranchManagerOnly")]
    public IActionResult Void(int id)
    {
        var transaction = _transactionService.GetAllTransactions().FirstOrDefault(x => x.Id == id);
        if (transaction is null)
        {
            return NotFound();
        }

        var staffIdValue = User?.FindFirst("StaffId")?.Value;
        var branchCode = User?.FindFirst("BranchCode")?.Value;
        if (string.IsNullOrWhiteSpace(staffIdValue) || string.IsNullOrWhiteSpace(branchCode))
        {
            return Forbid();
        }

        _transactionService.VoidTransaction(id, int.Parse(staffIdValue), "Manager approved void.", branchCode);
        return RedirectToAction(nameof(Index));
    }
}
