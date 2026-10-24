namespace ResourceManagementSystem.Utilities;

public interface IAuditStore
{
    Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
