using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ResourceManagementSystem.Utilities;

namespace ResourceManagementSystem.MVC.Controllers;

[ApiController]
[Route("integration")]
public class IntegrationController : ControllerBase
{
    [HttpGet("parcel-status")]
    [AllowAnonymous]
    [ApiKeyRequired]
    [EnableRateLimiting("partner")]
    public IActionResult ParcelStatus()
    {
        var payload = new
        {
            status = "InTransit",
            estimatedDelivery = DateTime.UtcNow.AddDays(2),
            trackingId = "APX-TRACK-2026-001",
            serviceLevel = "Express"
        };

        return Ok(payload);
    }
}
