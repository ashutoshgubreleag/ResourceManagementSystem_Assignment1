using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ResourceManagementSystem.Models.ValueObjects;
using ResourceManagementSystem.MVC.Models;
using ResourceManagementSystem.MVC.Security;
using ResourceManagementSystem.Services.Common;
using ResourceManagementSystem.Services.MoneyOrders;
using ResourceManagementSystem.Services.Transactions;
using ResourceManagementSystem.Utilities.Filters;

namespace ResourceManagementSystem.MVC.Controllers;

public sealed class MoneyOrderController(
    IMoneyOrderService moneyOrders,
    ITransactionService transactions,
    IOptions<BusinessRulesOptions> rules) : ApexPostController
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await moneyOrders.GetForBranchAsync(User.CanSeeAllBranches ? null : User.BranchCode, cancellationToken));

    [Authorize(Policy = Policies.CanTransact)]
    [HttpGet]
    public IActionResult Create(int id) =>
        View(new MoneyOrderFormViewModel { TransactionId = id, IdentityThreshold = rules.Value.MoneyOrderIdentityThreshold });

    [Authorize(Policy = Policies.CanTransact)]
    [HttpPost]
    [Audited("MoneyOrder.Add", "Amount")]
    public async Task<IActionResult> Create(int id, MoneyOrderFormViewModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        var order = await transactions.AddMoneyOrderAsync(id, new MoneyOrderCommand
        {
            Amount = model.Amount,
            Sender = model.Sender,
            Recipient = model.Recipient,
        }, Actor, cancellationToken);

        var threshold = new Money(rules.Value.MoneyOrderIdentityThreshold);
        Flash(order.Amount > threshold
            ? $"Money order {order.SerialNumber} added (fee {order.Fee}). It is above {threshold}: record a passed identity check before payment."
            : $"Money order {order.SerialNumber} added (fee {order.Fee}).");
        return RedirectToAction("Details", "Transaction", new { id });
    }

    [Authorize(Policy = Policies.CanTransact)]
    [HttpGet]
    public IActionResult Verify(int id) => View(new IdentityVerificationFormViewModel { TransactionId = id });

    /// <summary>
    /// Identity verification. Only DocumentType and Result are whitelisted for audit; the document
    /// number is masked in the service and never reaches the audit log.
    /// </summary>
    [Authorize(Policy = Policies.CanTransact)]
    [HttpPost]
    [Audited("IdentityVerification.Record", "DocumentType", "Result")]
    public async Task<IActionResult> Verify(int id, IdentityVerificationFormViewModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        var verification = await transactions.AddIdentityVerificationAsync(id, new IdentityVerificationCommand
        {
            CustomerName = model.CustomerName,
            DocumentType = model.DocumentType,
            DocumentNumber = model.DocumentNumber,
            Result = model.Result,
        }, Actor, cancellationToken);

        Flash($"Identity check recorded: {verification.DocumentType} {verification.MaskedDocumentNumber} ({verification.Result}).");
        return RedirectToAction("Details", "Transaction", new { id });
    }
}
