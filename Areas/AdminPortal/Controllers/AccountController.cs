using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    public async Task<IActionResult> Index(string? returnUrl)
    {
        if (await AdminIsSignedInAsync())
            return RedirectToAction("Index", "Dashboard", new { area = "AdminPortal" });

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(LoginViewModel model, string? returnUrl, CancellationToken cancellationToken)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
            return View(model);

        var admin = await _authentication.ValidateAdminAsync(model.Username, model.Password, cancellationToken);
        if (admin is null)
        {
            await _audit.WriteAsync("Admin", null, "Login failed", cancellationToken);
            _logger.LogWarning("Administrator login failed.");
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }

        await _audit.WriteAsync("Admin", admin.Uid, "Login succeeded", cancellationToken);
        await HttpContext.SignInAsync(AuthSchemes.Admin, CreatePrincipal(admin));

        if (ReturnUrlGuard.IsSafe(returnUrl, adminPortal: true))
            return Redirect(returnUrl!);

        return RedirectToAction("Index", "Dashboard", new { area = "AdminPortal" });
    }

    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await HttpContext.SignOutAsync(AuthSchemes.Admin);
        await _audit.WriteAsync("Admin", userId, "Logout", cancellationToken);
        return RedirectToAction(nameof(Index), new { area = "AdminPortal" });
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
            new("username", admin.Username)
        };

        if (!string.IsNullOrWhiteSpace(admin.Email))
            claims.Add(new Claim(ClaimTypes.Email, admin.Email));

        var identity = new ClaimsIdentity(claims, AuthSchemes.Admin);
        return new ClaimsPrincipal(identity);
    }
}
