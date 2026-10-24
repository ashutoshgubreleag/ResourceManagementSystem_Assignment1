using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using ResourceManagementSystem.Database.Models;
using ResourceManagementSystem.Repositories;
using ResourceManagementSystem.Services.Authentication;
using ResourceManagementSystem.Services.Branch;
using ResourceManagementSystem.Services.Catalog;
using ResourceManagementSystem.Services.Inventory;
using ResourceManagementSystem.Services.MoneyOrders;
using ResourceManagementSystem.Services.Reports;
using ResourceManagementSystem.Services.Shipping;
using ResourceManagementSystem.Services.Staff;
using ResourceManagementSystem.Services.Transactions;
using ResourceManagementSystem.Utilities;
using System.Net;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<IAuditStore, JsonAuditStore>();
builder.Services.AddScoped<AuditActionFilter>();
builder.Services.AddScoped<ValidationFilter>();
builder.Services.AddScoped<PerformanceFilter>();

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<AuditActionFilter>();
    options.Filters.Add<ValidationFilter>();
    options.Filters.Add<PerformanceFilter>();
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = null;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.IsEssential = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("BranchManagerOnly", policy =>
        policy.RequireRole(StaffRole.BranchManager.ToString()));

    options.AddPolicy("SameBranchOnly", policy =>
        policy.RequireAssertion(context =>
        {
            if (context.User.IsInRole(StaffRole.BranchManager.ToString()))
            {
                return true;
            }

            var userBranch = context.User.FindFirst("BranchCode")?.Value;
            var httpContext = context.Resource as HttpContext;
            var routeBranch = httpContext?.Request.RouteValues["branchCode"]?.ToString()
                ?? httpContext?.Request.RouteValues["BranchCode"]?.ToString();

            return !string.IsNullOrWhiteSpace(userBranch)
                && !string.IsNullOrWhiteSpace(routeBranch)
                && string.Equals(userBranch, routeBranch, StringComparison.OrdinalIgnoreCase);
        }));
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("partner", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "partner-unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromSeconds(30),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.AddScoped(typeof(IBaseEntityRepository<>), typeof(BaseJsonRepository<>));
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IBranchService, BranchService>();
builder.Services.AddScoped<IStaffService, StaffService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IShippingService, ShippingService>();
builder.Services.AddScoped<IMoneyOrderService, MoneyOrderService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IProductCatalogService, ProductCatalogService>();
builder.Services.AddUtilitiesExtentions();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseMiddleware<IpRestrictionMiddleware>();

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers["Content-Security-Policy"] = "default-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'none';";
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        return Task.CompletedTask;
    });

    await next();
});

app.UseMiddleware<MaintenanceModeMiddleware>();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseMiddleware<ApiKeyValidationMiddleware>();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

public partial class Program { }
