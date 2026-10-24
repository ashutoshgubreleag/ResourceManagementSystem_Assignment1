using ResourceManagementSystem.Database.Models;

namespace ResourceManagementSystem.Services.Reports;

public interface IReportService
{
    IEnumerable<BranchStockEntity> GetLowStockReport(string branchCode, int threshold);
    IEnumerable<TransactionEntity> GetDailyBranchSales(string branchCode, DateTime date);
    IEnumerable<ShippingOrderEntity> GetShippingVolumeByZone();
    IEnumerable<MoneyOrderEntity> GetMoneyOrdersByBranch(string branchCode);
}
