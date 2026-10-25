using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ResourceManagementSystem.MVC.Composition;
using ResourceManagementSystem.MVC.Models;
using ResourceManagementSystem.MVC.Security;
using ResourceManagementSystem.Utilities.Filters;
using AppAuthenticationService = ResourceManagementSystem.Services.Authentication.IAuthenticationService;

namespace ResourceManagementSystem.MVC.Controllers;

public sealed class AccountController(AppAuthenticationService authentication) : ApexPostController
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null) =>
        User.Identity?.IsAuthenticated == true
            ? RedirectToAction("Index", "Home")
            : View(new LoginViewModel { ReturnUrl = returnUrl });

    /// <summary>Strict rate limit (brute-force protection); antiforgery is enforced globally.</summary>
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(WebServiceRegistration.LoginRateLimitPolicy)]
    [Audited("Account.Login", "Email")]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        var staff = await authentication.AuthenticateAsync(model.Email, model.Password, cancellationToken);
        if (staff is null)
        {
            // Same message for unknown email, wrong password and inactive account.
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            model.Password = string.Empty;
            return View(model);
        }

        var principal = ClaimsPrincipalExtensions.CreatePrincipal(staff, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties { IsPersistent = false });

        // The cookie only takes effect on the next request; set the user now so the audit entry
        // for this login records who signed in.
        HttpContext.User = principal;

        // LocalRedirect: never follow an attacker-supplied absolute returnUrl (open redirect).
        return !string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)
            ? LocalRedirect(model.ReturnUrl)
            : RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied() => StatusCode(StatusCodes.Status403Forbidden);
}
