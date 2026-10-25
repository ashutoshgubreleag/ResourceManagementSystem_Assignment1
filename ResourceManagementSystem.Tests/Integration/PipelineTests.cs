using System.Net;
using System.Text.Json;
using ResourceManagementSystem.Tests.Support;
using static ResourceManagementSystem.Tests.Support.ApexPostWebApplicationFactory;

namespace ResourceManagementSystem.Tests.Integration;

/// <summary>Middleware pipeline behaviour, end to end through the real app (WebApplicationFactory).</summary>
public sealed class PipelineTests : IClassFixture<ApexPostWebApplicationFactory>
{
    private readonly ApexPostWebApplicationFactory _factory;

    public PipelineTests(ApexPostWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task SecurityHeaders_ArePresent()
    {
        using var client = _factory.CreateBrowser();

        using var response = await client.GetAsync(new Uri("/Account/Login", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("default-src 'self'", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.False(response.Headers.Contains("Server"));
    }

    [Fact]
    public async Task SecurityHeaders_AreAlsoOnErrorResponses()
    {
        using var client = _factory.CreateBrowser();

        using var response = await client.GetAsync(new Uri("/integration/parcel-status/APX1", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
    }

    [Fact]
    public async Task CorrelationId_IsGenerated_WhenNotSupplied()
    {
        using var client = _factory.CreateBrowser();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        var id = Assert.Single(response.Headers.GetValues("X-Correlation-Id"));
        Assert.Equal(32, id.Length);
    }

    [Fact]
    public async Task CorrelationId_IsEchoed_WhenSupplied()
    {
        using var client = _factory.CreateBrowser();
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "partner-trace-42");

        using var response = await client.GetAsync(new Uri("/Account/Login", UriKind.Relative));

        Assert.Equal("partner-trace-42", Assert.Single(response.Headers.GetValues("X-Correlation-Id")));
    }

    [Fact]
    public async Task CorrelationId_UnsafeValuesAreReplaced()
    {
        using var client = _factory.CreateBrowser();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Correlation-Id", "bad value with spaces <script>");

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.NotEqual("bad value with spaces <script>", Assert.Single(response.Headers.GetValues("X-Correlation-Id")));
    }

    [Fact]
    public async Task Health_IsAnonymousAndHealthy()
    {
        using var client = _factory.CreateBrowser();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UnauthenticatedAccess_RedirectsToLogin()
    {
        using var client = _factory.CreateBrowser();

        using var response = await client.GetAsync(new Uri("/Transaction", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("https://localhost/Account/Login", response.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FormPost_WithoutAntiforgeryToken_IsRejected()
    {
        using var client = _factory.CreateBrowser();
        using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["Email"] = "admin@apexpost.local", ["Password"] = "ApexPost123!" });

        using var response = await client.PostAsync(new Uri("/Account/Login", UriKind.Relative), content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AuthCookie_IsHttpOnlySecureAndStrict()
    {
        using var client = _factory.CreateBrowser();

        using var response = await LoginAsync(client, "clerk.br001a@apexpost.local");

        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("__Host-ApexPost.Auth", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnhandledException_ReturnsProblemDetails_WithoutInternals()
    {
        using var client = _factory.CreateBrowser();
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");

        using var response = await client.GetAsync(new Uri("/Throw/Now", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.TryGetProperty("traceId", out _));
        Assert.True(json.RootElement.TryGetProperty("correlationId", out _));
        Assert.DoesNotContain("secret", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at ResourceManagementSystem", body, StringComparison.Ordinal); // no stack trace
    }

    [Fact]
    public async Task UnhandledException_ShowsFriendlyPageToBrowsers()
    {
        using var client = _factory.CreateBrowser();

        using var response = await client.GetAsync(new Uri("/Throw/Now", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("Something went wrong", body, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", body, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class MaintenanceModeTests : IDisposable
{
    private readonly ApexPostWebApplicationFactory _factory = ApexPostWebApplicationFactory.With(new() { ["MaintenanceMode:Enabled"] = "true", ["MaintenanceMode:RetryAfterSeconds"] = "120" });

    [Fact]
    public async Task Returns503WithRetryAfter_ButKeepsHealthAvailable()
    {
        using var client = _factory.CreateBrowser();

        using var page = await client.GetAsync(new Uri("/Account/Login", UriKind.Relative));
        using var health = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, page.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(120), page.Headers.RetryAfter?.Delta);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    public void Dispose() => _factory.Dispose();
}
