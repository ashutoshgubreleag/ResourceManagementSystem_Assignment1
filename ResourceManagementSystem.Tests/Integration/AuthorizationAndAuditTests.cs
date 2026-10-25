using System.Net;
using System.Text.Json;
using ResourceManagementSystem.Tests.Support;
using static ResourceManagementSystem.Tests.Support.ApexPostWebApplicationFactory;

namespace ResourceManagementSystem.Tests.Integration;

public sealed class LoginRateLimitTests : IDisposable
{
    private readonly ApexPostWebApplicationFactory _factory = ApexPostWebApplicationFactory.With(new() { ["RateLimiting:LoginPermitLimit"] = "5" });

    [Fact]
    public async Task Login_Returns429_OnceTheLimitIsExceeded()
    {
        using var client = _factory.CreateBrowser("192.0.2.10");
        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 6; attempt++)
        {
            using var response = await LoginAsync(client, "admin@apexpost.local", "wrong-password");
            statuses.Add(response.StatusCode);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                Assert.NotNull(response.Headers.RetryAfter);
            }
        }

        Assert.All(statuses.Take(5), s => Assert.Equal(HttpStatusCode.OK, s)); // wrong password redisplays the form
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[5]);

        // A different client IP has its own budget.
        using var other = _factory.CreateBrowser("192.0.2.11");
        using var ok = await LoginAsync(other, "admin@apexpost.local");
        Assert.Equal(HttpStatusCode.Redirect, ok.StatusCode);
    }

    public void Dispose() => _factory.Dispose();
}

public sealed class AuthorizationAndAuditTests : IClassFixture<ApexPostWebApplicationFactory>
{
    private readonly ApexPostWebApplicationFactory _factory;

    public AuthorizationAndAuditTests(ApexPostWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Clerk_Gets403_OnVoid()
    {
        using var clerk = _factory.CreateBrowser();
        await LoginAsync(clerk, "clerk.br001a@apexpost.local");
        var txId = await OpenBasketAsync(clerk);

        using var response = await PostFormAsync(clerk, $"/Transaction/Details/{txId}", $"/Transaction/Void/{txId}", new() { ["Reason"] = "Clerk should not be able to" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task InvalidCredentials_ShowGenericError()
    {
        using var client = _factory.CreateBrowser();

        using var response = await LoginAsync(client, "former.clerk@apexpost.local"); // inactive account, right password

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Invalid email or password.", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Clerk_CannotOpenAnotherBranchDashboard()
    {
        using var clerk = _factory.CreateBrowser();
        await LoginAsync(clerk, "clerk.br001a@apexpost.local");

        using var own = await clerk.GetAsync(new Uri("/Branch/Dashboard/BR-001", UriKind.Relative));
        using var other = await clerk.GetAsync(new Uri("/Branch/Dashboard/BR-002", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode); // SameBranchOnly policy
    }

    [Fact]
    public async Task Clerk_CannotSeeTheAuditorView()
    {
        using var clerk = _factory.CreateBrowser();
        await LoginAsync(clerk, "clerk.br001a@apexpost.local");

        using var response = await clerk.GetAsync(new Uri("/Report/MoneyOrders", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Auditor_SeesAllBranches_AndTheAuditLog()
    {
        using var auditor = _factory.CreateBrowser();
        await LoginAsync(auditor, "auditor@apexpost.local");

        using var dashboard = await auditor.GetAsync(new Uri("/Branch/Dashboard/BR-004", UriKind.Relative));
        using var audit = await auditor.GetAsync(new Uri("/Admin/Audit", UriKind.Relative));
        using var json = await auditor.GetAsync(new Uri("/Report/MoneyOrders?format=json&from=2026-10-01&to=2026-10-31", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
        Assert.Equal(HttpStatusCode.OK, audit.StatusCode);
        Assert.Equal("application/json", json.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ReportWithBadDate_Returns400()
    {
        using var clerk = _factory.CreateBrowser();
        await LoginAsync(clerk, "clerk.br001a@apexpost.local");

        using var response = await clerk.GetAsync(new Uri("/Report/DailySales?date=31-10-2026", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnknownTransaction_Returns404()
    {
        using var clerk = _factory.CreateBrowser();
        await LoginAsync(clerk, "clerk.br001a@apexpost.local");

        using var response = await clerk.GetAsync(new Uri("/Transaction/Details/999999", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ClosedBranchClerk_CannotOpenABasket()
    {
        using var clerk = _factory.CreateBrowser();
        await LoginAsync(clerk, "clerk.br006a@apexpost.local");

        using var response = await PostFormAsync(clerk, "/Transaction/Create", "/Transaction/Create", new() { ["CounterTerminalId"] = "1" });
        using var page = await clerk.GetAsync(response.Headers.Location!);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode); // PRG back to the form, not to a new basket
        Assert.DoesNotContain("/Transaction/Details/", response.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.Contains("closed", await page.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FullSale_IsAudited_WithoutSensitiveData()
    {
        using var clerk = _factory.CreateBrowser();
        clerk.DefaultRequestHeaders.Add("X-Correlation-Id", "audit-flow-test-1");
        await LoginAsync(clerk, "clerk.br001b@apexpost.local", "ApexPost123!");
        var txId = await OpenBasketAsync(clerk);
        var page = $"/Transaction/Details/{txId}";

        using var mo = await PostFormAsync(clerk, $"/MoneyOrder/Create/{txId}", $"/MoneyOrder/Create/{txId}", new() { ["Amount"] = "1800", ["Sender"] = "Ann", ["Recipient"] = "Ben" });
        using var blocked = await PostFormAsync(clerk, page, $"/Transaction/Pay/{txId}", new() { ["PaymentMethod"] = "Card" });
        using var verify = await PostFormAsync(clerk, $"/MoneyOrder/Verify/{txId}", $"/MoneyOrder/Verify/{txId}", new()
        {
            ["CustomerName"] = "Ann Customer",
            ["DocumentType"] = "Passport",
            ["DocumentNumber"] = "PX7766554433",
            ["Result"] = "Passed",
        });
        using var pay = await PostFormAsync(clerk, page, $"/Transaction/Pay/{txId}", new() { ["PaymentMethod"] = "Card" });
        var details = await clerk.GetStringAsync(new Uri(page, UriKind.Relative));

        Assert.Equal(HttpStatusCode.Redirect, pay.StatusCode);
        Assert.Contains(">Paid<", details, StringComparison.Ordinal);
        Assert.Contains("****4433", details, StringComparison.Ordinal);

        var lines = await File.ReadAllLinesAsync(_factory.AuditFile);
        var entries = lines.Select(l => JsonDocument.Parse(l).RootElement).Where(e => e.GetProperty("CorrelationId").GetString() == "audit-flow-test-1").ToList();
        var actions = entries.Select(e => e.GetProperty("Action").GetString()).ToList();
        Assert.Contains("Account.Login", actions);
        Assert.Contains("MoneyOrder.Add", actions);
        Assert.Contains("IdentityVerification.Record", actions);

        var pays = entries.Where(e => e.GetProperty("Action").GetString() == "Transaction.Pay").ToList();
        Assert.Equal(2, pays.Count);
        Assert.Equal("Rejected", pays[0].GetProperty("Outcome").GetString());
        Assert.Equal("MONEY_ORDER_ID_REQUIRED", pays[0].GetProperty("Error").GetString());
        Assert.Equal("Succeeded", pays[1].GetProperty("Outcome").GetString());
        Assert.All(entries, e =>
        {
            Assert.Equal("8", e.GetProperty("StaffId").GetString());
            Assert.Equal("BR-001", e.GetProperty("BranchCode").GetString());
        });

        var raw = string.Join("\n", lines);
        Assert.DoesNotContain("PX7766554433", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("ApexPost123!", raw, StringComparison.Ordinal);
        Assert.DoesNotContain(ApexPostWebApplicationFactory.TestApiKey, raw, StringComparison.Ordinal);

        // Also not in the data files.
        foreach (var file in Directory.GetFiles(_factory.DataDirectory, "*.json"))
        {
            Assert.DoesNotContain("PX7766554433", await File.ReadAllTextAsync(file), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Manager_CanVoid_WithAReason_AndItIsAudited()
    {
        using var clerk = _factory.CreateBrowser();
        await LoginAsync(clerk, "clerk.br003a@apexpost.local");
        var txId = await OpenBasketAsync(clerk);

        using var manager = _factory.CreateBrowser();
        manager.DefaultRequestHeaders.Add("X-Correlation-Id", "void-test-1");
        await LoginAsync(manager, "manager.br003@apexpost.local");
        var page = $"/Transaction/Details/{txId}";

        using var noReason = await PostFormAsync(manager, page, $"/Transaction/Void/{txId}", new() { ["Reason"] = "" });
        var stillOpen = await manager.GetStringAsync(new Uri(page, UriKind.Relative));
        using var voided = await PostFormAsync(manager, page, $"/Transaction/Void/{txId}", new() { ["Reason"] = "Customer walked away" });
        var after = await manager.GetStringAsync(new Uri(page, UriKind.Relative));

        Assert.Equal(HttpStatusCode.Redirect, noReason.StatusCode);
        Assert.Contains(">Open<", stillOpen, StringComparison.Ordinal);
        Assert.Contains(">Voided<", after, StringComparison.Ordinal);
        Assert.Contains("Customer walked away", after, StringComparison.Ordinal);

        var audit = (await File.ReadAllLinesAsync(_factory.AuditFile))
            .Select(l => JsonDocument.Parse(l).RootElement)
            .Where(e => e.GetProperty("CorrelationId").GetString() == "void-test-1" && e.GetProperty("Action").GetString() == "Transaction.Void")
            .ToList();
        Assert.Equal(2, audit.Count);
        Assert.Equal("Rejected", audit[0].GetProperty("Outcome").GetString());
        Assert.Equal("Succeeded", audit[1].GetProperty("Outcome").GetString());
        Assert.Equal("BranchManager", audit[1].GetProperty("Role").GetString());
        Assert.Equal("Customer walked away", audit[1].GetProperty("Fields").GetProperty("Reason").GetString());
    }

    [Fact]
    public async Task ActionResponses_CarryServerTiming()
    {
        using var clerk = _factory.CreateBrowser();
        await LoginAsync(clerk, "clerk.br001a@apexpost.local");

        using var response = await clerk.GetAsync(new Uri("/Catalog", UriKind.Relative));

        Assert.StartsWith("action;dur=", response.Headers.GetValues("Server-Timing").Single(), StringComparison.Ordinal);
    }

    private static async Task<string> OpenBasketAsync(HttpClient client)
    {
        using var response = await PostFormAsync(client, "/Transaction/Create", "/Transaction/Create", new() { ["CounterTerminalId"] = "1" });
        var location = response.Headers.Location!.ToString();
        Assert.Contains("/Transaction/Details/", location, StringComparison.Ordinal);
        return location[(location.LastIndexOf('/') + 1)..];
    }
}
