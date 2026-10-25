using System.Text;
using System.Text.Json;

namespace ResourceManagementSystem.Utilities.Audit;

/// <summary>
/// Append-only JSON Lines audit store (one JSON object per line).
/// <list type="bullet">
/// <item>Appending a line never rewrites earlier entries (the Week 1 store re-serialised the whole
/// file on every write, which is slow and lets a crash corrupt history).</item>
/// <item>Singleton with one <see cref="SemaphoreSlim"/>, so concurrent requests cannot interleave
/// partial lines. The old store used <c>lock</c> on a <i>scoped</i> instance, which locked nothing.</item>
/// <item>Fully async file I/O.</item>
/// </list>
/// </summary>
public sealed class JsonAuditStore : IAuditStore, IDisposable
{
    private static readonly JsonSerializerOptions LineOptions = new() { WriteIndented = false };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _filePath;

    public JsonAuditStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public string FilePath => _filePath;

    public async Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var line = JsonSerializer.Serialize(entry, LineOptions) + "\n";
        var bytes = Encoding.UTF8.GetBytes(line);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var stream = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<AuditEntry>> ReadRecentAsync(int take, CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        var entries = new List<AuditEntry>();
        await using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync: true);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (line.Length > 0 && JsonSerializer.Deserialize<AuditEntry>(line, LineOptions) is { } entry)
            {
                entries.Add(entry);
            }
        }

        return entries.OrderByDescending(e => e.TimestampUtc).Take(take).ToList();
    }

    public void Dispose() => _gate.Dispose();
}
