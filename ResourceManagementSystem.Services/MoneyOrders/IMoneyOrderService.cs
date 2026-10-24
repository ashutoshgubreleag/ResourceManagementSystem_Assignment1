using ResourceManagementSystem.Database.Models;

namespace ResourceManagementSystem.Services.MoneyOrders;

public interface IMoneyOrderService
{
    MoneyOrderEntity CreateMoneyOrder(MoneyOrderEntity moneyOrder, IdentityVerificationEntity? verification = null, decimal verificationThreshold = 1000m);
    bool RequiresIdentityVerification(decimal amount, decimal threshold);
    IdentityVerificationEntity AddIdentityVerification(IdentityVerificationEntity verification);
}
