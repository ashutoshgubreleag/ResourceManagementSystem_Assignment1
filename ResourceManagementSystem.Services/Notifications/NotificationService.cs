using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Models.ValueObjects;
using ResourceManagementSystem.Repositories;
using ResourceManagementSystem.Repositories.Specifications;

namespace ResourceManagementSystem.Services.Notifications;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationEntity>> GetRecentAsync(BranchCode? branch, int take, CancellationToken cancellationToken);
}

public sealed class NotificationService(IBaseEntityRepository<NotificationEntity> notifications) : INotificationService
{
    public async Task<IReadOnlyList<NotificationEntity>> GetRecentAsync(BranchCode? branch, int take, CancellationToken cancellationToken)
    {
        var spec = branch is { } code
            ? Specification<NotificationEntity>.Where(n => n.BranchCode == code, "branch")
            : Specification<NotificationEntity>.All;

        var items = await notifications.FindAsync(spec, cancellationToken).ConfigureAwait(false);
        return items.OrderByDescending(n => n.CreatedAt).Take(take).ToList();
    }
}
