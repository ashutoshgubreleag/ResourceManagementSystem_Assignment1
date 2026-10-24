using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Repositories;

namespace ResourceManagementSystem.Services.Reports;

public class ReportService : IReportService
{
    private readonly IBaseEntityRepository<BranchStockEntity> _stockRepository;
    private readonly IBaseEntityRepository<TransactionEntity> _transactionRepository;
    private readonly IBaseEntityRepository<ShippingOrderEntity> _shippingRepository;
    private readonly IBaseEntityRepository<MoneyOrderEntity> _moneyOrderRepository;

    public ReportService(
        IBaseEntityRepository<BranchStockEntity> stockRepository,
        IBaseEntityRepository<TransactionEntity> transactionRepository,
        IBaseEntityRepository<ShippingOrderEntity> shippingRepository,
        IBaseEntityRepository<MoneyOrderEntity> moneyOrderRepository)
    {
        _stockRepository = stockRepository;
        _shippingRepository = shippingRepository;
        _transactionRepository = transactionRepository;
        _moneyOrderRepository = moneyOrderRepository;
    }

    public IEnumerable<BranchStockEntity> GetLowStockReport(string branchCode, int threshold)
    {
        return _stockRepository.GetAll()
            .Where(item => string.Equals(item.BranchCode, branchCode, StringComparison.OrdinalIgnoreCase)
                && item.QuantityOnHand <= Math.Max(item.ReorderThreshold, threshold));
    }

    public IEnumerable<TransactionEntity> GetDailyBranchSales(string branchCode, DateTime date)
    {
        return _transactionRepository.GetAll()
            .Where(transaction => string.Equals(transaction.BranchCode, branchCode, StringComparison.OrdinalIgnoreCase)
                && transaction.CreatedAt.Date == date.Date
                && transaction.Status == TransactionStatus.Paid);
    }

    public IEnumerable<ShippingOrderEntity> GetShippingVolumeByZone()
    {
        return _shippingRepository.GetAll();
    }

    public IEnumerable<MoneyOrderEntity> GetMoneyOrdersByBranch(string branchCode)
    {
        return _moneyOrderRepository.GetAll()
            .Where(order => string.Equals(order.BranchCode, branchCode, StringComparison.OrdinalIgnoreCase));
    }
}
