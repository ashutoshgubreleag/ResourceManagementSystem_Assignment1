using System.Text.Json;

namespace ResourceManagementSystem.Utilities;

public sealed class JsonAuditStore : IAuditStore
{
    private readonly object _syncRoot = new();
    private readonly string _filePath;

    public JsonAuditStore()
    {
        _filePath = ResolveAuditPath();
    }

    public Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var entries = ReadEntries();
            entries.Add(entry);

            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_filePath, json);
        }

        return Task.CompletedTask;
    }

    private List<AuditEntry> ReadEntries()
    {
        if (!File.Exists(_filePath))
        {
            return new List<AuditEntry>();
        }

        var json = File.ReadAllText(_filePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<AuditEntry>();
        }

        return JsonSerializer.Deserialize<List<AuditEntry>>(json) ?? new List<AuditEntry>();
    }

    private static string ResolveAuditPath()
    {
        var candidateDirectories = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "JSONs"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ResourceManagementSystem.Database", "JSONs"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "ResourceManagementSystem.Database", "JSONs")
        };

        foreach (var directory in candidateDirectories)
        {
            var fullDirectory = Path.GetFullPath(directory);
            var candidate = Path.Combine(fullDirectory, "audit-log.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var databaseRoot = FindDatabaseRoot();
        var fallbackDirectory = Path.Combine(databaseRoot, "JSONs");
        Directory.CreateDirectory(fallbackDirectory);
        return Path.Combine(fallbackDirectory, "audit-log.json");
    }

    private static string FindDatabaseRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "ResourceManagementSystem.Database");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return AppContext.BaseDirectory;
    }
}
