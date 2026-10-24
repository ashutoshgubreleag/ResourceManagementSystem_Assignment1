using ResourceManagementSystem.Database.Models;

namespace ResourceManagementSystem.Services.Inventory;

public interface IInventoryService
{
    BranchStockEntity AddStock(BranchStockEntity stockItem);
    IEnumerable<BranchStockEntity> GetAllStock();
    IEnumerable<BranchStockEntity> GetStockForBranch(string branchCode);
    int GetAvailableQuantity(string branchCode, string sku);
    void ReserveStock(string branchCode, string sku, int quantity);
    IEnumerable<BranchStockEntity> GetLowStockItems(string branchCode, int threshold);
}
