using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ApexPost.MVC.Security;
using ApexPost.Repositories.Json;
using ApexPost.Services;
using ApexPost.Utilities;
using ApexPost.Utilities.Filters;
using ApexPost.Utilities.Middleware;

namespace ApexPost.MVC.Composition;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int LoginPermitLimit { get; set; } = 5;
    public int LoginWindowSeconds { get; set; } = 60;
    public int PartnerPermitLimit { get; set; } = 60;
    public int PartnerWindowSeconds { get; set; } = 60;
}

/// <summary>Service registration for the web app, kept out of Program.cs so Program.cs reads as a composition root.</summary>
public static class WebServiceRegistration
{
    public const string LoginRateLimitPolicy = "login";
    public const string PartnerRateLimitPolicy = "partner";
    public const string PartnerCorsPolicy = "PartnerTrackingWidget";

    public static IServiceCollection AddApexPostWeb(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddApexPostServices(configuration, environment.ContentRootPath);

        // The audit log lives next to the JSON data files.
        services.AddApexPostUtilities(configuration, sp => sp.GetRequiredService<JsonStorageSettings>().DataDirectory);

        services.AddControllersWithViews(options =>
        {
            // Order: audit outermost so it records the final outcome of validation and the action.
            options.Filters.AddService<AuditActionFilter>(order: -100);
            options.Filters.AddService<PerformanceFilter>(order: -90);
            options.Filters.AddService<ValidationFilter>(order: -80);
            options.Filters.AddService<DomainExceptionFilter>();
            options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()); // every unsafe-method form post
        });

        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = "__Host-ApexPost.Antiforgery";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.HttpOnly = true;
        });

        // TempData backs Post/Redirect/Get messages; keep its cookie as locked down as the auth cookie.
        services.Configure<CookieTempDataProviderOptions>(options =>
        {
            options.Cookie.Name = "__Host-ApexPost.TempData";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.HttpOnly = true;
        });

        AddAuthentication(services);
        services.AddAuthorization(Policies.Configure);
        services.AddSingleton<IAuthorizationHandler, SameBranchHandler>();

        AddRateLimiting(services, configuration);
        AddCors(services, configuration);

        services.AddHealthChecks().AddCheck<StorageHealthCheck>("json-storage");

        // Only trust X-Forwarded-* from known proxies: the framework default (loopback, i.e. a reverse
        // proxy on the same machine) plus any listed in configuration. NEVER clear both lists: with no
        // known proxies or networks ForwardedHeaders trusts every caller, and a forged X-Forwarded-For
        // would walk straight past the IP allowlist (there is a test for exactly that).
        services.AddOptions<ForwardedHeadersOptions>().Configure(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            foreach (var proxy in configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }
        });

        services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(365);
            options.IncludeSubDomains = true;
        });

        return services;
    }

    private static void AddAuthentication(IServiceCollection services)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Account/Login";
                options.LogoutPath = "/Account/Logout";
                options.AccessDeniedPath = "/Account/AccessDenied";
                options.Cookie.Name = "__Host-ApexPost.Auth"; // __Host- prefix: Secure, Path=/, no Domain
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.ExpireTimeSpan = TimeSpan.FromHours(8); // one counter shift
                options.SlidingExpiration = true;
                options.Events = new CookieAuthenticationEvents
                {
                    // A signed-in user without permission gets a real 403 (rendered by the status
                    // code page), not a 302 that looks like success to scripts and tests.
                    OnRedirectToAccessDenied = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    },
                    OnRedirectToLogin = context =>
                    {
                        if (context.Request.Path.StartsWithSegments("/integration", StringComparison.OrdinalIgnoreCase))
                        {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return Task.CompletedTask;
                        }

                        context.Response.Redirect(context.RedirectUri);
                        return Task.CompletedTask;
                    },
                };
            });
    }

    private static void AddRateLimiting(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimitingOptions>(configuration.GetSection(RateLimitingOptions.SectionName));

        // Limits are read per partition from IOptions, i.e. after the host is built.
        static RateLimitingOptions Limits(HttpContext http) =>
            http.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<RateLimitingOptions>>().Value;

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                await ProblemResponses.WriteAsync(context.HttpContext, new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests",
                    Detail = "Rate limit exceeded. Wait before trying again.",
                }).ConfigureAwait(false);
            };

            // Brute-force protection: a few login attempts per client IP per window, no queueing.
            options.AddPolicy(LoginRateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Limits(http).LoginPermitLimit,
                    Window = TimeSpan.FromSeconds(Limits(http).LoginWindowSeconds),
                    QueueLimit = 0,
                }));

            // Per partner (identified by the validated API key), so one partner cannot starve another
            // and rotating source IPs does not reset the budget.
            options.AddPolicy(PartnerRateLimitPolicy, http => RateLimitPartition.GetTokenBucketLimiter(
                http.Items[ApiKeyValidationMiddleware.PartnerItemKey] as string ?? "unidentified",
                _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = Limits(http).PartnerPermitLimit,
                    TokensPerPeriod = Limits(http).PartnerPermitLimit,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(Limits(http).PartnerWindowSeconds),
                    QueueLimit = 0,
                }));
        });
    }

    private static void AddCors(IServiceCollection services, IConfiguration configuration)
    {
        services.AddCors(options => options.AddPolicy(PartnerCorsPolicy, policy => policy
            .WithOrigins(configuration.GetSection("Cors:PartnerTrackingWidgetOrigins").Get<string[]>() ?? [])
            .WithMethods(HttpMethods.Get)
            .WithHeaders("X-Api-Key", "X-Correlation-Id")
            .WithExposedHeaders("X-Correlation-Id")));
    }
}

/// <summary>Health check: the JSON data directory exists and is writable.</summary>
public sealed class StorageHealthCheck(JsonStorageSettings settings) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var probe = Path.Combine(settings.DataDirectory, ".health");
            await File.WriteAllTextAsync(probe, DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy("JSON storage is writable.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return HealthCheckResult.Unhealthy("JSON storage is not writable.");
        }
    }
}
