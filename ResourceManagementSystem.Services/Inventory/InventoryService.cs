using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Repositories;

namespace ResourceManagementSystem.Services.Inventory;

public class InventoryService : IInventoryService
{
    private readonly IBaseEntityRepository<BranchStockEntity> _stockRepository;

    public InventoryService(IBaseEntityRepository<BranchStockEntity> stockRepository)
    {
        _stockRepository = stockRepository;
    }

    public BranchStockEntity AddStock(BranchStockEntity stockItem)
    {
        if (stockItem == null) throw new ArgumentNullException(nameof(stockItem));
        if (string.IsNullOrWhiteSpace(stockItem.BranchCode)) throw new InvalidOperationException("Branch code is required.");
        if (string.IsNullOrWhiteSpace(stockItem.Sku)) throw new InvalidOperationException("SKU is required.");

        var existing = _stockRepository.GetAll().FirstOrDefault(item =>
            string.Equals(item.BranchCode, stockItem.BranchCode, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.Sku, stockItem.Sku, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            existing.QuantityOnHand += stockItem.QuantityOnHand;
            existing.UpdatedAt = DateTime.UtcNow;
            var updated = _stockRepository.Update(existing);
            if (!updated) throw new InvalidOperationException("Unable to update branch stock.");
            return existing;
        }

        stockItem.UpdatedAt = DateTime.UtcNow;
        return _stockRepository.Add(stockItem);
    }

    public IEnumerable<BranchStockEntity> GetStockForBranch(string branchCode)
    {
        if (string.IsNullOrWhiteSpace(branchCode)) return Enumerable.Empty<BranchStockEntity>();
        return _stockRepository.GetAll()
            .Where(item => string.Equals(item.BranchCode, branchCode, StringComparison.OrdinalIgnoreCase));
    }

    public int GetAvailableQuantity(string branchCode, string sku)
    {
        if (string.IsNullOrWhiteSpace(branchCode) || string.IsNullOrWhiteSpace(sku)) return 0;

        return _stockRepository.GetAll()
            .Where(item => string.Equals(item.BranchCode, branchCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.Sku, sku, StringComparison.OrdinalIgnoreCase))
            .Sum(item => item.QuantityOnHand);
    }

    public void ReserveStock(string branchCode, string sku, int quantity)
    {
        if (quantity < 0) throw new InvalidOperationException("Reserved quantity cannot be negative.");
        var available = GetAvailableQuantity(branchCode, sku);
        if (available < quantity)
            throw new InvalidOperationException($"Insufficient stock for SKU '{sku}' at branch '{branchCode}'.");

        var item = _stockRepository.GetAll().FirstOrDefault(stock =>
            string.Equals(stock.BranchCode, branchCode, StringComparison.OrdinalIgnoreCase)
            && string.Equals(stock.Sku, sku, StringComparison.OrdinalIgnoreCase));

        if (item == null) throw new InvalidOperationException($"No stock found for SKU '{sku}'.");

        item.QuantityOnHand -= quantity;
        item.UpdatedAt = DateTime.UtcNow;
        var updated = _stockRepository.Update(item);
        if (!updated) throw new InvalidOperationException("Unable to reserve stock.");
    }

    public IEnumerable<BranchStockEntity> GetLowStockItems(string branchCode, int threshold)
    {
        if (threshold < 0) threshold = 0;
        return GetStockForBranch(branchCode)
            .Where(item => item.QuantityOnHand <= Math.Max(item.ReorderThreshold, threshold));
    }
}
