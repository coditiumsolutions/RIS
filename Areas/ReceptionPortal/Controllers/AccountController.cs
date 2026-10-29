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
    private readonly LoginFailureRateLimiter _loginRateLimiter;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        PortalAuthentication authentication,
        AuditLogger audit,
        LoginFailureRateLimiter loginRateLimiter,
        ILogger<AccountController> logger)
    {
        _authentication = authentication;
        _audit = audit;
        _loginRateLimiter = loginRateLimiter;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet]
    [HttpHead]
    public async Task<IActionResult> Index(string? returnUrl)
    {
        if (HttpMethods.IsHead(Request.Method))
            return Ok();

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

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        if (_loginRateLimiter.IsLimited("Receptionist", clientIp))
        {
            ModelState.AddModelError(string.Empty, "Too many login attempts. Please try again later.");
            return View(model);
        }

        var (result, receptionist, lockoutRemaining) = await _authentication.ValidateReceptionistAsync(model.Username, model.Password, cancellationToken);

        switch (result)
        {
            case LoginResult.AccountLocked:
                await _audit.WriteAsync("Receptionist", null, "Login blocked - account locked", cancellationToken);
                var minutes = lockoutRemaining.HasValue ? (int)Math.Ceiling(lockoutRemaining.Value.TotalMinutes) : 15;
                ModelState.AddModelError(string.Empty, $"Account is locked. Please try again in {minutes} minute(s).");
                return View(model);

            case LoginResult.AccountInactive:
                await _audit.WriteAsync("Receptionist", null, "Login blocked - account inactive", cancellationToken);
                ModelState.AddModelError(string.Empty, "Your account has been deactivated. Please contact administration.");
                return View(model);

            case LoginResult.InvalidCredentials:
                _loginRateLimiter.RecordFailure("Receptionist", clientIp);
                await _audit.WriteAsync("Receptionist", null, "Login failed", cancellationToken);
                _logger.LogWarning("Receptionist login failed.");
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                return View(model);
        }

        await _audit.WriteAsync("Receptionist", receptionist!.Uid, "Login succeeded", cancellationToken);
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

        if (userId.HasValue)
        {
            var stampService = HttpContext.RequestServices.GetRequiredService<SecurityStampService>();
            await stampService.RotateStampAsync("Receptionist", userId.Value, cancellationToken);
        }

        await HttpContext.SignOutAsync(AuthSchemes.Receptionist);
        await _audit.WriteAsync("Receptionist", userId, "Logout", cancellationToken);
        return Redirect("/ReceptionPortal");
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
            new("MustChangePassword", receptionist.MustChangePassword.ToString()),
            new("SecurityStamp", receptionist.SecurityStamp ?? string.Empty)
        };

        if (!string.IsNullOrWhiteSpace(receptionist.Email))
            claims.Add(new Claim(ClaimTypes.Email, receptionist.Email));

        var identity = new ClaimsIdentity(claims, AuthSchemes.Receptionist);
        return new ClaimsPrincipal(identity);
    }
}
