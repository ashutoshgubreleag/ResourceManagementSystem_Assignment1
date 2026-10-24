using Microsoft.AspNetCore.HttpOverrides;
using ResourceManagementSystem.Repositories;
using ResourceManagementSystem.Utilities;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
//services.AddDbContext<ApplicationDbContext>(opts => opts.UseSqlite("Data Source=ResourceManagementSystem.db"));
//services.AddScoped(typeof(IBaseEntityRepository<>), typeof(BaseEntityRepository<>));
// To use JSON-backed repository for all entities, replace BaseEntityRepository registration with BaseJsonRepository:
builder.Services.AddScoped(typeof(IBaseEntityRepository<>), typeof(BaseJsonRepository<>));
builder.Services.AddUtilitiesExtentions();

//If your application runs behind IIS, Nginx, a load balancer, or another reverse proxy, configure forwarded headers only for proxies you actually trust.
//builder.Services.Configure<ForwardedHeadersOptions>(options =>
//{
//    options.ForwardedHeaders =
//        ForwardedHeaders.XForwardedFor |
//        ForwardedHeaders.XForwardedProto;

//    // Replace with the actual IP address of your reverse proxy.
//    options.KnownProxies.Add(
//        IPAddress.Parse("10.0.0.10"));
//});

var app = builder.Build();

// 1.Correlation ID must be established first.
app.UseMiddleware<CorrelationIdMiddleware>();

// 2. Log the completed request, including failures.
app.UseMiddleware<RequestLoggingMiddleware>();

// 3. Exception handling middleware should be placed after logging middleware to ensure that exceptions are logged.
app.UseExceptionHandler();

// 4. Process forwarded headers from trusted proxies only.
// Register this before middleware that relies on client IP or scheme.
// app.UseForwardedHeaders();

// 5. Enable HTTP Strict Transport Security (HSTS) outside Development
//    to instruct browsers to use HTTPS for subsequent requests.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// 6. Redirect incoming HTTP requests to HTTPS.
app.UseHttpsRedirection();

// 7. Restrict administrative and integration endpoints by client IP.
app.UseMiddleware<IpRestrictionMiddleware>();

// 8. Apply security headers to outgoing HTTP responses.
//    Use OnStarting to ensure headers are set before the response is sent.
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;

        // Restrict resource loading to the same origin and disable
        // plugins and framing to mitigate common web-based attacks.
        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "object-src 'none'; " +
            "base-uri 'self'; " +
            "frame-ancestors 'none';";

        // Prevent browsers from MIME-sniffing the response content type.
        headers["X-Content-Type-Options"] = "nosniff";

        // Prevent the application from being embedded in frames,
        // helping protect against clickjacking attacks.
        headers["X-Frame-Options"] = "DENY";

        // Limit referrer information shared with other origins.
        headers["Referrer-Policy"] =
            "strict-origin-when-cross-origin";

        return Task.CompletedTask;
    });

    await next();
});

// 9. Block application requests while maintenance is enabled.
app.UseMiddleware<MaintenanceModeMiddleware>();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
