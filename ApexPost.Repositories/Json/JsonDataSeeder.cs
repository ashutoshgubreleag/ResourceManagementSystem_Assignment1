namespace ApexPost.Repositories.Json;

/// <summary>
/// Copies the shipped seed files into the data directory the first time the app runs.
/// Runtime writes then go to the data directory, so the committed seed data never changes.
/// </summary>
public static class JsonDataSeeder
{
    public static async Task<int> EnsureSeededAsync(JsonStorageSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Directory.CreateDirectory(settings.DataDirectory);

        if (!Directory.Exists(settings.SeedDirectory))
        {
            return 0;
        }

        var copied = 0;
        foreach (var seedFile in Directory.EnumerateFiles(settings.SeedDirectory, "*Entity.json"))
        {
            var target = Path.Combine(settings.DataDirectory, Path.GetFileName(seedFile));
            if (File.Exists(target))
            {
                continue;
            }

            await using var source = new FileStream(seedFile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            await using var destination = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true);
            await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            copied++;
        }

        return copied;
    }
}
