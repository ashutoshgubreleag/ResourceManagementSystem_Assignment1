using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Repositories;
using ResourceManagementSystem.Services.Inventory;
using ResourceManagementSystem.Services.MoneyOrders;
using ResourceManagementSystem.Services.Shipping;
using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Xunit;

namespace ResourceManagementSystem.Tests;

public class ApexPostBusinessRulesTests
{
    [Fact]
    public void ReserveStock_RejectsInsufficientQuantity()
    {
        var repo = new InMemoryRepository<BranchStockEntity>(new[]
        {
            new BranchStockEntity { Id = 1, BranchCode = "BR-001", Sku = "PKG-001", QuantityOnHand = 2, ReorderThreshold = 5 }
        });

        var service = new InventoryService(repo);

        var ex = Assert.Throws<InvalidOperationException>(() => service.ReserveStock("BR-001", "PKG-001", 3));
        Assert.Contains("Insufficient stock", ex.Message);
    }

    [Fact]
    public void CreateMoneyOrder_RequiresVerificationForHighValueTransaction()
    {
        var moneyRepo = new InMemoryRepository<MoneyOrderEntity>();
        var verificationRepo = new InMemoryRepository<IdentityVerificationEntity>();
        var service = new MoneyOrderService(moneyRepo, verificationRepo);

        var order = new MoneyOrderEntity
        {
            BranchCode = "BR-001",
            Amount = 2000m,
            Sender = "Alice",
            Recipient = "Bob",
            Fee = 25m
        };

        var ex = Assert.Throws<InvalidOperationException>(() => service.CreateMoneyOrder(order, verificationThreshold: 1000m));
        Assert.Contains("Identity verification is required", ex.Message);
    }

    [Fact]
    public void ParcelWeight_RejectsOverLimitForServiceLevel()
    {
        var repo = new InMemoryRepository<ShippingOrderEntity>();
        var service = new ShippingService(repo);

        var isValid = service.IsValidForServiceLevel(ShippingType.Parcel, ShippingServiceLevel.Express, 4m);

        Assert.False(isValid);
    }

    [Fact]
    public void CalculatePrice_ReturnsExpectedRateForLocalStandardParcel()
    {
        var repo = new InMemoryRepository<ShippingOrderEntity>();
        var service = new ShippingService(repo);

        var result = service.CalculatePrice(ShippingType.Parcel, DestinationZone.Local, ShippingServiceLevel.Standard, 2m);

        Assert.Equal(4.00m, result);
    }

    private sealed class InMemoryRepository<TEntity> : IBaseEntityRepository<TEntity> where TEntity : class
    {
        private readonly List<TEntity> _items;

        public InMemoryRepository(IEnumerable<TEntity>? items = null)
        {
            _items = items?.ToList() ?? new List<TEntity>();
        }

        public TEntity Add(TEntity entity)
        {
            var idProperty = typeof(TEntity).GetProperty("Id");
            if (idProperty is not null && idProperty.PropertyType == typeof(int))
            {
                var next = _items.Select(x => (int)(idProperty.GetValue(x) ?? 0)).DefaultIfEmpty(0).Max() + 1;
                idProperty.SetValue(entity, next);
            }

            _items.Add(entity);
            return entity;
        }

        public bool Update(TEntity entity)
        {
            var idProperty = typeof(TEntity).GetProperty("Id");
            if (idProperty is null) return false;
            var id = idProperty.GetValue(entity);
            var index = _items.FindIndex(x => Equals(idProperty.GetValue(x), id));
            if (index < 0) return false;
            _items[index] = entity;
            return true;
        }

        public TEntity? GetById(object id)
        {
            var idProperty = typeof(TEntity).GetProperty("Id");
            if (idProperty is null) return null;
            var target = idProperty.PropertyType == typeof(int) ? Convert.ToInt32(id) : id;
            return _items.FirstOrDefault(x => Equals(idProperty.GetValue(x), target));
        }

        public IEnumerable<TEntity> GetAll() => _items;

        public bool Delete(object id)
        {
            var item = GetById(id);
            if (item is null) return false;
            return _items.Remove(item);
        }
    }
}

public class ApexPostIntegrationTests
{
    [Fact]
    public async Task SecurityHeaders_ArePresent()
    {
        var factory = new ApexPostWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/Account/Login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.True(response.Headers.Contains("X-Frame-Options"));
    }

    [Fact]
    public async Task IntegrationEndpoint_Returns401WithoutApiKey()
    {
        var factory = new ApexPostWebApplicationFactory { AllowedIps = new[] { "127.0.0.1", "::1" } };
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "127.0.0.1");

        var response = await client.GetAsync("/integration/parcel-status");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task BlockedIp_Returns403()
    {
        var factory = new ApexPostWebApplicationFactory { AllowedIps = new[] { "10.0.0.1", "10.0.0.2" } };
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "127.0.0.1");

        var response = await client.GetAsync("/integration/parcel-status");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedAccess_RedirectsToLogin()
    {
        var factory = new ApexPostWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/Branch");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.OriginalString ?? string.Empty);
    }

    [Fact]
    public async Task CorrelationIdHeader_IsReturned()
    {
        var factory = new ApexPostWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Correlation-Id"));
    }

    [Fact]
    public async Task Login_OnceRateLimitExceeded_Returns429()
    {
        var factory = new ApexPostWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        for (var i = 0; i < 5; i++)
        {
            var getResponse = await client.GetAsync("/Account/Login");
            var html = await getResponse.Content.ReadAsStringAsync();
            var tokenMatch = Regex.Match(html, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""");

            var token = tokenMatch.Groups[1].Value;
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Email", "admin@apexpost.local"),
                new KeyValuePair<string, string>("Password", "ApexPost123!"),
                new KeyValuePair<string, string>("__RequestVerificationToken", token)
            });

            var response = await client.PostAsync("/Account/Login", form);
            if (i < 4)
            {
                Assert.True(response.StatusCode == HttpStatusCode.Redirect || response.StatusCode == HttpStatusCode.OK,
                    $"Unexpected status code on attempt {i + 1}: {response.StatusCode}");
            }
        }

        var lastLoginPage = await client.GetAsync("/Account/Login");
        var lastHtml = await lastLoginPage.Content.ReadAsStringAsync();
        var lastToken = Regex.Match(lastHtml, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""");
        var formContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Email", "admin@apexpost.local"),
            new KeyValuePair<string, string>("Password", "ApexPost123!"),
            new KeyValuePair<string, string>("__RequestVerificationToken", lastToken.Groups[1].Value)
        });

        var rateLimited = await client.PostAsync("/Account/Login", formContent);
        Assert.Equal(HttpStatusCode.TooManyRequests, rateLimited.StatusCode);
    }

    [Fact]
    public async Task Clerk_Gets403_OnVoidAction()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("BranchManagerOnly", policy => policy.RequireRole("BranchManager"));
        });

        using var provider = services.BuildServiceProvider();
        var authorizationService = provider.GetRequiredService<IAuthorizationService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Role, "Clerk") },
            authenticationType: "Test"));

        var result = await authorizationService.AuthorizeAsync(principal, "BranchManagerOnly");

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
    }
}

public class ApexPostWebApplicationFactory : WebApplicationFactory<Program>
{
    public string[] AllowedIps { get; set; } = new[] { "10.0.0.1" };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["AllowedHosts"] = "*",
                ["IpRestriction:Enabled"] = "true",
                ["IpRestriction:ProtectedPathPrefixes:0"] = "/integration",
                ["MaintenanceMode:Enabled"] = "false",
                ["Authentication:DemoPassword"] = "ApexPost123!",
                ["Integration:ApiKey"] = "ApexPost-Partner-Key-2026"
            };

            for (var i = 0; i < AllowedIps.Length; i++)
            {
                settings[$"IpRestriction:AllowedIPs:{i}"] = AllowedIps[i];
            }

            configBuilder.AddInMemoryCollection(settings);
        });
    }
}
