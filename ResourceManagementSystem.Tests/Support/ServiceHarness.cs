using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Models.ValueObjects;
using ResourceManagementSystem.Repositories.Json;
using ResourceManagementSystem.Services;
using ResourceManagementSystem.Services.Common;

namespace ResourceManagementSystem.Tests.Support;

/// <summary>A controllable clock so time-based rules are deterministic.</summary>
public sealed class FakeClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>
/// Real service graph (AddApexPostServices) over a private copy of the seed data in a temp folder,
/// so business-rule tests exercise the actual services and JSON repositories, not mocks.
/// </summary>
public sealed class ServiceHarness : IAsyncDisposable
{
    private readonly ServiceProvider _root;

    private ServiceHarness(ServiceProvider root, string dataDirectory, FakeClock clock)
    {
        _root = root;
        DataDirectory = dataDirectory;
        Clock = clock;
    }

    public string DataDirectory { get; }
    public FakeClock Clock { get; }

    public static async Task<ServiceHarness> CreateAsync(IDictionary<string, string?>? overrides = null)
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), "apexpost-tests", Guid.NewGuid().ToString("N"));
        var settings = new Dictionary<string, string?>
        {
            ["Storage:DataDirectory"] = dataDirectory,
            ["Courier:RateLookupLatencyMs"] = "1",
            ["Courier:TrackingLatencyMs"] = "1",
            ["Courier:TrackingTimeoutMs"] = "200",
        };
        foreach (var (key, value) in overrides ?? new Dictionary<string, string?>())
        {
            settings[key] = value;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var clock = new FakeClock(new DateTimeOffset(2026, 10, 25, 3, 0, 0, TimeSpan.Zero));
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<TimeProvider>(clock);
        services.AddApexPostServices(configuration, AppContext.BaseDirectory);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await JsonDataSeeder.EnsureSeededAsync(provider.GetRequiredService<JsonStorageSettings>(), CancellationToken.None);
        return new ServiceHarness(provider, dataDirectory, clock);
    }

    public T Get<T>() where T : notnull => _root.GetRequiredService<T>();

    /// <summary>Resolves a scoped service in a fresh scope (like one HTTP request).</summary>
    public T Scoped<T>() where T : notnull => _root.CreateScope().ServiceProvider.GetRequiredService<T>();

    public static StaffContext Clerk(int id = 7, string branch = "BR-001") => new(id, StaffRole.CounterClerk, new BranchCode(branch), "Test clerk");

    public static StaffContext Manager(int id = 6, string branch = "BR-001") => new(id, StaffRole.BranchManager, new BranchCode(branch), "Test manager");

    public async ValueTask DisposeAsync()
    {
        await _root.DisposeAsync();
        try
        {
            Directory.Delete(DataDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Best effort clean-up of the temp folder.
        }
    }
}
