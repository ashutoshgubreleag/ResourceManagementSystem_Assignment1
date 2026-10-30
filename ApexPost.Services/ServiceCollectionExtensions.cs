using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ApexPost.Repositories;
using ApexPost.Repositories.Json;
using ApexPost.Services.Authentication;
using ApexPost.Services.Branch;
using ApexPost.Services.Caching;
using ApexPost.Services.Catalog;
using ApexPost.Services.Common;
using ApexPost.Services.Courier;
using ApexPost.Services.Dashboard;
using ApexPost.Services.Events;
using ApexPost.Services.Inventory;
using ApexPost.Services.MoneyOrders;
using ApexPost.Services.Notifications;
using ApexPost.Services.Reports;
using ApexPost.Services.Shipping;
using ApexPost.Services.Staff;
using ApexPost.Services.Transactions;

namespace ApexPost.Services;

/// <summary>
/// Registers the repositories and services. Lifetimes (see DESIGN.md, ADR-W2-07):
/// <list type="bullet">
/// <item><b>Singleton</b>: repositories (one SemaphoreSlim + snapshot per file), the reference-data
/// cache, the event hub, locks, the courier clients (they own a throttle), business rules.</item>
/// <item><b>Scoped</b>: business services. They are stateless today, but scoped keeps them safe to
/// gain per-request state (a unit of work next week) without a lifetime audit.</item>
/// </list>
/// Singletons never depend on scoped services, and ValidateScopes is on in Development to prove it.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApexPostServices(this IServiceCollection services, IConfiguration configuration, string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Storage: resolved lazily (configuration can still change until the host is built, e.g. in
        // tests) and relative paths are anchored to the content root so they are stable per environment.
        services.AddSingleton(_ =>
        {
            var storage = configuration.GetSection(JsonStorageSettings.SectionName).Get<JsonStorageSettings>() ?? new JsonStorageSettings();
            if (!Path.IsPathRooted(storage.DataDirectory))
            {
                storage.DataDirectory = Path.GetFullPath(Path.Combine(contentRootPath, storage.DataDirectory));
            }

            return storage;
        });
        services.AddSingleton(typeof(IBaseEntityRepository<>), typeof(BaseJsonRepository<>));
        services.AddHostedService<JsonSeedHostedService>();

        services.TryAddTimeProvider();
        services.AddMemoryCache();
        services.Configure<BusinessRulesOptions>(configuration.GetSection(BusinessRulesOptions.SectionName));
        services.Configure<CourierOptions>(configuration.GetSection(CourierOptions.SectionName));
        services.AddSingleton(sp => BusinessRules.FromOptions(sp.GetRequiredService<IOptions<BusinessRulesOptions>>()));

        services.AddSingleton<IApexPostEvents, ApexPostEventHub>();
        services.AddSingleton<BranchLockProvider>();
        services.AddSingleton<IReferenceDataCache, ReferenceDataCache>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ICourierRateService, SimulatedCourierRateService>();
        services.AddSingleton<ICourierTrackingService, SimulatedCourierTrackingService>();
        services.AddSingleton<IShippingPricingStrategy, DefaultShippingPricingStrategy>();
        services.AddHostedService<NotificationDispatcher>();

        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<IStaffService, StaffService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IProductCatalogService, ProductCatalogService>();
        services.AddScoped<IShippingService, ShippingService>();
        services.AddScoped<IMoneyOrderService, MoneyOrderService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }

    private static void TryAddTimeProvider(this IServiceCollection services)
    {
        if (!services.Any(d => d.ServiceType == typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }
}

/// <summary>Copies seed files into the data directory before the app starts serving requests.</summary>
internal sealed partial class JsonSeedHostedService(JsonStorageSettings settings, ILogger<JsonSeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var copied = await JsonDataSeeder.EnsureSeededAsync(settings, cancellationToken).ConfigureAwait(false);
        LogSeeded(copied, settings.DataDirectory);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded {Count} JSON data file(s) into {DataDirectory}")]
    private partial void LogSeeded(int count, string dataDirectory);
}
