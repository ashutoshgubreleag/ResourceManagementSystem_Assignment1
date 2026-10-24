using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Repositories;

namespace ResourceManagementSystem.Services.MoneyOrders;

public class MoneyOrderService : IMoneyOrderService
{
    private readonly IBaseEntityRepository<MoneyOrderEntity> _moneyOrderRepository;
    private readonly IBaseEntityRepository<IdentityVerificationEntity> _verificationRepository;

    public MoneyOrderService(
        IBaseEntityRepository<MoneyOrderEntity> moneyOrderRepository,
        IBaseEntityRepository<IdentityVerificationEntity> verificationRepository)
    {
        _moneyOrderRepository = moneyOrderRepository;
        _verificationRepository = verificationRepository;
    }

    public IdentityVerificationEntity AddIdentityVerification(IdentityVerificationEntity verification)
    {
        if (verification == null) throw new ArgumentNullException(nameof(verification));
        if (string.IsNullOrWhiteSpace(verification.MaskedDocumentNumber))
            throw new InvalidOperationException("Masked document number is required.");

        return _verificationRepository.Add(verification);
    }

    public bool RequiresIdentityVerification(decimal amount, decimal threshold) => amount > threshold;

    public MoneyOrderEntity CreateMoneyOrder(MoneyOrderEntity moneyOrder, IdentityVerificationEntity? verification = null, decimal verificationThreshold = 1000m)
    {
        if (moneyOrder == null) throw new ArgumentNullException(nameof(moneyOrder));
        if (string.IsNullOrWhiteSpace(moneyOrder.Sender)) throw new InvalidOperationException("Sender is required.");
        if (string.IsNullOrWhiteSpace(moneyOrder.Recipient)) throw new InvalidOperationException("Recipient is required.");

        if (RequiresIdentityVerification(moneyOrder.Amount, verificationThreshold))
        {
            if (verification == null || verification.Result != VerificationResult.Pass)
                throw new InvalidOperationException("Identity verification is required for high-value money orders.");

            moneyOrder.IdentityVerificationId = verification.Id;
        }

        moneyOrder.Status = MoneyOrderStatus.Approved;
        return _moneyOrderRepository.Add(moneyOrder);
    }
}
