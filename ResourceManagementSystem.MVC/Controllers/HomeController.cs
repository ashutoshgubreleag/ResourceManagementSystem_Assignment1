using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.MVC.Models;
using ResourceManagementSystem.Services.Branch;
using ResourceManagementSystem.Services.Catalog;
using ResourceManagementSystem.Services.Inventory;
using ResourceManagementSystem.Services.Staff;
using ResourceManagementSystem.Services.Transactions;
using System.Diagnostics;

namespace ResourceManagementSystem.MVC.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly IBranchService _branchService;
        private readonly IStaffService _staffService;
        private readonly IProductCatalogService _productCatalogService;
        private readonly IInventoryService _inventoryService;
        private readonly ITransactionService _transactionService;

        public HomeController(
            IBranchService branchService,
            IStaffService staffService,
            IProductCatalogService productCatalogService,
            IInventoryService inventoryService,
            ITransactionService transactionService)
        {
            _branchService = branchService;
            _staffService = staffService;
            _productCatalogService = productCatalogService;
            _inventoryService = inventoryService;
            _transactionService = transactionService;
        }

        public IActionResult Index()
        {
            var model = new DashboardViewModel
            {
                BranchCount = _branchService.GetAll().Count(),
                StaffCount = _staffService.GetAll().Count(),
                ProductCount = _productCatalogService.GetProducts().Count(),
                ActiveTransactionCount = _transactionService.GetAllTransactions().Count(x => x.Status == TransactionStatus.Paid),
                LowStockCount = _inventoryService.GetAllStock().Count(x => x.QuantityOnHand <= x.ReorderThreshold)
            };

            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
