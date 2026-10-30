using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ApexPost.MVC.Composition;
using ApexPost.Services.Courier;
using ApexPost.Utilities;

namespace ApexPost.MVC.Controllers;

/// <summary>
/// Courier partner integration. No staff cookie: partners authenticate with an API key, enforced by
/// middleware from the <see cref="ApiKeyRequiredAttribute"/> metadata, and are also IP-restricted
/// (/integration prefix), rate limited per partner and CORS-limited to the tracking widget origin.
/// </summary>
[JsonApi]
[AllowAnonymous]
[ApiKeyRequired]
[EnableRateLimiting(WebServiceRegistration.PartnerRateLimitPolicy)]
[EnableCors(WebServiceRegistration.PartnerCorsPolicy)]
public sealed class IntegrationController(ICourierTrackingService tracking) : Controller
{
    [HttpGet]
    [SkipStatusCodePages]
    public async Task<IActionResult> ParcelStatus(string trackingNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber) || trackingNumber.Length > 32 || !trackingNumber.All(char.IsAsciiLetterOrDigit))
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "Invalid tracking number" });
        }

        var result = await tracking.TrackAsync(trackingNumber, cancellationToken);

        // Pattern matching on the result type maps each outcome to the right status code.
        return result switch
        {
            TrackingResult.Found found => Json(new
            {
                trackingNumber = found.TrackingNumber,
                status = found.Status,
                lastScanLocation = found.LastScanLocation,
                estimatedDelivery = found.EstimatedDelivery,
            }),
            TrackingResult.NotFound notFound => NotFound(new ProblemDetails
            {
                Status = 404,
                Title = "Unknown tracking number",
                Detail = $"No shipment with tracking number {notFound.TrackingNumber}.",
            }),
            TrackingResult.Unavailable unavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = 503,
                Title = "Tracking temporarily unavailable",
                Detail = unavailable.Reason,
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }
}
