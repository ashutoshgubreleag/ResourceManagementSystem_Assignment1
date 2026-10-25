using ResourceManagementSystem.Models.ValueObjects;

namespace ResourceManagementSystem.Database.Models;

/// <summary>A simulated notification (would be an email/SMS to the branch manager in production).</summary>
public class NotificationEntity : IEntity
{
    public int Id { get; set; }
    public NotificationType Type { get; set; }
    public BranchCode? BranchCode { get; set; }
    public string Recipient { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
