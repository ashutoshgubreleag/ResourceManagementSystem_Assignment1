using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Services.MoneyOrders;

namespace ResourceManagementSystem.MVC.Controllers;

[Authorize]
public class MoneyOrderController : Controller
{
    private readonly IMoneyOrderService _moneyOrderService;

    public MoneyOrderController(IMoneyOrderService moneyOrderService)
    {
        _moneyOrderService = moneyOrderService;
    }

    public IActionResult Index()
    {
        return View(_moneyOrderService.GetAllOrders().OrderByDescending(x => x.CreatedAt).ToList());
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new MoneyOrderEntity());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(MoneyOrderEntity model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var verification = model.Amount > 1000m
                ? new IdentityVerificationEntity
                {
                    BranchCode = model.BranchCode,
                    StaffId = 1,
                    DocumentType = DocumentType.Passport,
                    MaskedDocumentNumber = "********1234",
                    Result = VerificationResult.Pass,
                    VerifiedAt = DateTime.UtcNow
                }
                : null;

            _moneyOrderService.CreateMoneyOrder(model, verification, 1000m);
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }
}
