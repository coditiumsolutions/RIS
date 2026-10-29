using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResidentPortal.Models;
using ResidentPortal.Security;
using ResidentPortal.Services;

namespace ResidentPortal.Areas.ReceptionPortal.Controllers;

[Area("ReceptionPortal")]
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
        if (await ReceptionistIsSignedInAsync())
            return RedirectToAction("Index", "Dashboard", new { area = "ReceptionPortal" });

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

        var receptionist = await _authentication.ValidateReceptionistAsync(model.Username, model.Password, cancellationToken);
        if (receptionist is null)
        {
            await _audit.WriteAsync("Receptionist", null, "Login failed", cancellationToken);
            _logger.LogWarning("Receptionist login failed.");
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }

        await _audit.WriteAsync("Receptionist", receptionist.Uid, "Login succeeded", cancellationToken);
        await HttpContext.SignInAsync(AuthSchemes.Receptionist, CreatePrincipal(receptionist));

        if (receptionist.MustChangePassword)
            return RedirectToAction("Index", "Password", new { area = "ReceptionPortal", mustChange = true });

        if (ReturnUrlGuard.IsSafe(returnUrl, PortalType.Receptionist))
            return Redirect(returnUrl!);

        return RedirectToAction("Index", "Dashboard", new { area = "ReceptionPortal" });
    }

    [Authorize(Policy = AuthPolicies.ReceptionistOnly)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await HttpContext.SignOutAsync(AuthSchemes.Receptionist);
        await _audit.WriteAsync("Receptionist", userId, "Logout", cancellationToken);
        return RedirectToAction(nameof(Index), new { area = "ReceptionPortal" });
    }

    private async Task<bool> ReceptionistIsSignedInAsync()
    {
        var result = await HttpContext.AuthenticateAsync(AuthSchemes.Receptionist);
        return result.Succeeded && result.Principal?.IsInRole(PortalRoles.Receptionist) == true;
    }

    private static ClaimsPrincipal CreatePrincipal(Receptionist receptionist)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, receptionist.Uid.ToString()),
            new(ClaimTypes.Name, receptionist.FullName),
            new(ClaimTypes.Role, PortalRoles.Receptionist),
            new("username", receptionist.Username),
            new("MustChangePassword", receptionist.MustChangePassword.ToString())
        };

        if (!string.IsNullOrWhiteSpace(receptionist.Email))
            claims.Add(new Claim(ClaimTypes.Email, receptionist.Email));

        var identity = new ClaimsIdentity(claims, AuthSchemes.Receptionist);
        return new ClaimsPrincipal(identity);
    }
}
