using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResidentPortal.Models;
using ResidentPortal.Security;
using ResidentPortal.Services;

namespace ResidentPortal.Areas.Staff.Controllers;

[Area("Staff")]
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

        var staffAuth = await HttpContext.AuthenticateAsync(AuthSchemes.Staff);
        if (staffAuth.Succeeded
            && (staffAuth.Principal?.IsInRole(PortalRoles.Admin) == true
                || staffAuth.Principal?.IsInRole(PortalRoles.Receptionist) == true))
        {
            return RedirectAfterLogin(staffAuth.Principal.GetStaffUserType()
                ?? (staffAuth.Principal.IsInRole(PortalRoles.Receptionist)
                    ? PortalRoles.Receptionist
                    : PortalRoles.Admin));
        }

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
        if (_loginRateLimiter.IsLimited("staff", clientIp))
        {
            ModelState.AddModelError(string.Empty, "Too many login attempts. Please try again later.");
            return View(model);
        }

        var (result, staff, lockoutRemaining) = await _authentication.ValidateStaffAsync(
            model.Username, model.Password, cancellationToken);

        switch (result)
        {
            case LoginResult.AmbiguousUsername:
                _logger.LogError(
                    "Staff login refused: username '{Username}' exists in both AdminUsers and Receptionists.",
                    model.Username.Trim());
                await _audit.WriteAsync("Staff", null,
                    $"Login refused - ambiguous username '{model.Username.Trim()}' exists in AdminUsers and Receptionists",
                    cancellationToken);
                ModelState.AddModelError(string.Empty,
                    "This username is configured for more than one staff account type. Contact an administrator.");
                return View(model);

            case LoginResult.AccountLocked:
                await _audit.WriteAsync("Staff", null, "Login blocked - account locked", cancellationToken);
                var minutes = lockoutRemaining.HasValue ? (int)Math.Ceiling(lockoutRemaining.Value.TotalMinutes) : 15;
                ModelState.AddModelError(string.Empty, $"Account is locked. Please try again in {minutes} minute(s).");
                return View(model);

            case LoginResult.AccountInactive:
                await _audit.WriteAsync("Staff", null, "Login blocked - account inactive", cancellationToken);
                ModelState.AddModelError(string.Empty, "Your account has been deactivated. Please contact administration.");
                return View(model);

            case LoginResult.InvalidCredentials:
                _loginRateLimiter.RecordFailure("staff", clientIp);
                await _audit.WriteAsync("Staff", null, "Login failed", cancellationToken);
                _logger.LogWarning("Staff login failed.");
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                return View(model);
        }

        await _audit.WriteAsync(staff!.UserType, staff.Uid, "Login succeeded", cancellationToken);
        await HttpContext.SignInAsync(
            AuthSchemes.Staff,
            StaffPrincipalFactory.Create(
                staff.Uid,
                staff.FullName,
                staff.UserType,
                staff.Username,
                staff.Email,
                staff.SecurityStamp,
                staff.MustChangePassword));

        if (staff.MustChangePassword)
            return RedirectToAction("Index", "Password", new { area = "Staff", mustChange = true });

        if (ReturnUrlGuard.IsSafe(returnUrl, PortalType.Staff))
            return Redirect(returnUrl!);

        return RedirectAfterLogin(staff.UserType);
    }

    [Authorize(Policy = AuthPolicies.StaffOnly, AuthenticationSchemes = AuthSchemes.Staff)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var userType = User.GetStaffUserType() ?? "Staff";

        if (userId.HasValue &&
            (string.Equals(userType, PortalRoles.Admin, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(userType, PortalRoles.Receptionist, StringComparison.OrdinalIgnoreCase)))
        {
            var stampService = HttpContext.RequestServices.GetRequiredService<SecurityStampService>();
            await stampService.RotateStampAsync(userType, userId.Value, cancellationToken);
        }

        await HttpContext.SignOutAsync(AuthSchemes.Staff);
        await _audit.WriteAsync(userType, userId, "Logout", cancellationToken);
        return Redirect("/Staff");
    }

    private IActionResult RedirectAfterLogin(string? userType)
    {
        if (string.Equals(userType, PortalRoles.Receptionist, StringComparison.OrdinalIgnoreCase))
            return RedirectToAction("Index", "Desk", new { area = "Staff" });

        return RedirectToAction("Index", "Dashboard", new { area = "Staff" });
    }
}
