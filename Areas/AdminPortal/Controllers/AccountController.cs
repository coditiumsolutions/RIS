using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ResidentPortal.Models;
using ResidentPortal.Security;
using ResidentPortal.Services;

namespace ResidentPortal.Areas.AdminPortal.Controllers;

[Area("AdminPortal")]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class AccountController : Controller
{
    private readonly PortalAuthentication _authentication;
    private readonly AuditLogger _audit;
    private readonly ILogger<AccountController> _logger;

    public AccountController(PortalAuthentication authentication, AuditLogger audit, ILogger<AccountController> logger)
    {
        _authentication = authentication;
        _audit = audit;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet]
    [HttpHead]
    public async Task<IActionResult> Index(string? returnUrl)
    {
        if (HttpMethods.IsHead(Request.Method))
            return Ok();

        if (await AdminIsSignedInAsync())
            return RedirectToAction("Index", "Dashboard", new { area = "AdminPortal" });

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Index(LoginViewModel model, string? returnUrl, CancellationToken cancellationToken)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
            return View(model);

        var (result, admin, lockoutRemaining) = await _authentication.ValidateAdminAsync(model.Username, model.Password, cancellationToken);

        switch (result)
        {
            case LoginResult.AccountLocked:
                await _audit.WriteAsync("Admin", null, "Login blocked - account locked", cancellationToken);
                var minutes = lockoutRemaining.HasValue ? (int)Math.Ceiling(lockoutRemaining.Value.TotalMinutes) : 15;
                ModelState.AddModelError(string.Empty, $"Account is locked. Please try again in {minutes} minute(s).");
                return View(model);

            case LoginResult.AccountInactive:
                await _audit.WriteAsync("Admin", null, "Login blocked - account inactive", cancellationToken);
                ModelState.AddModelError(string.Empty, "Your account has been deactivated. Please contact administration.");
                return View(model);

            case LoginResult.InvalidCredentials:
                await _audit.WriteAsync("Admin", null, "Login failed", cancellationToken);
                _logger.LogWarning("Administrator login failed.");
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                return View(model);
        }

        await _audit.WriteAsync("Admin", admin!.Uid, "Login succeeded", cancellationToken);
        await HttpContext.SignInAsync(AuthSchemes.Admin, CreatePrincipal(admin));

        if (admin.MustChangePassword)
            return RedirectToAction("Index", "Password", new { area = "AdminPortal", mustChange = true });

        if (ReturnUrlGuard.IsSafe(returnUrl, adminPortal: true))
            return Redirect(returnUrl!);

        return RedirectToAction("Index", "Dashboard", new { area = "AdminPortal" });
    }

    [Authorize(Policy = AuthPolicies.AdminOnly, AuthenticationSchemes = AuthSchemes.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId.HasValue)
        {
            var stampService = HttpContext.RequestServices.GetRequiredService<SecurityStampService>();
            await stampService.RotateStampAsync("Admin", userId.Value, cancellationToken);
        }

        await HttpContext.SignOutAsync(AuthSchemes.Admin);
        await _audit.WriteAsync("Admin", userId, "Logout", cancellationToken);
        return Redirect("/AdminPortal");
    }

    private async Task<bool> AdminIsSignedInAsync()
    {
        var result = await HttpContext.AuthenticateAsync(AuthSchemes.Admin);
        return result.Succeeded && result.Principal?.IsInRole(PortalRoles.Admin) == true;
    }

    private static ClaimsPrincipal CreatePrincipal(AdminUser admin)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, admin.Uid.ToString()),
            new(ClaimTypes.Name, admin.FullName),
            new(ClaimTypes.Role, PortalRoles.Admin),
            new("username", admin.Username),
            new("SecurityStamp", admin.SecurityStamp ?? string.Empty)
        };

        if (!string.IsNullOrWhiteSpace(admin.Email))
            claims.Add(new Claim(ClaimTypes.Email, admin.Email));

        var identity = new ClaimsIdentity(claims, AuthSchemes.Admin);
        return new ClaimsPrincipal(identity);
    }
}
