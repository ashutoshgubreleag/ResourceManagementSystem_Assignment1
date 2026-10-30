namespace ApexPost.Repositories.Json;

/// <summary>Where the JSON "database" lives.</summary>
public sealed class JsonStorageSettings
{
    public const string SectionName = "Storage";

    /// <summary>Folder for the live data files (relative paths are resolved by the host).</summary>
    public string DataDirectory { get; set; } = "App_Data";

    /// <summary>Folder containing the read-only seed files shipped with the build.</summary>
    public string SeedDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "SeedData");

    /// <summary>File name for an entity type, e.g. <c>TransactionEntity.json</c>.</summary>
    public static string FileNameFor(Type entityType) => entityType.Name + ".json";
}
