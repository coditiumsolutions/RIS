using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResidentPortal.Models;
using ResidentPortal.Security;
using ResidentPortal.Services;

namespace ResidentPortal.Controllers;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class HomeController : Controller
{
    private readonly PortalAuthentication _authentication;
    private readonly AuditLogger _audit;
    private readonly LoginFailureRateLimiter _loginRateLimiter;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        PortalAuthentication authentication,
        AuditLogger audit,
        LoginFailureRateLimiter loginRateLimiter,
        ILogger<HomeController> logger)
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

        // "/" shows Resident Portal login for anonymous users; signed-in residents keep the home grid.
        if (await ResidentIsSignedInAsync())
        {
            ViewData["Title"] = "Home";
            return View();
        }

        ViewData["ReturnUrl"] = returnUrl;
        ViewData["Title"] = "Resident Login";
        return View("Login", new LoginViewModel());
    }

    [AllowAnonymous]
    [HttpGet("/login")]
    [HttpHead("/login")]
    public async Task<IActionResult> Login(string? returnUrl)
    {
        if (HttpMethods.IsHead(Request.Method))
            return Ok();

        if (await ResidentIsSignedInAsync())
            return RedirectToAction(nameof(Index), new { area = "" });

        ViewData["ReturnUrl"] = returnUrl;
        ViewData["Title"] = "Resident Login";
        return View(new LoginViewModel());
    }

    [AllowAnonymous]
    [HttpPost("/login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl, CancellationToken cancellationToken)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
            return View(model);

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        if (_loginRateLimiter.IsLimited("Resident", clientIp))
        {
            ModelState.AddModelError(string.Empty, "Too many login attempts. Please try again later.");
            return View(model);
        }

        var (result, resident, lockoutRemaining) = await _authentication.ValidateResidentAsync(model.Username, model.Password, cancellationToken);

        switch (result)
        {
            case LoginResult.AccountLocked:
                await _audit.WriteAsync("Resident", null, "Login blocked - account locked", cancellationToken);
                var minutes = lockoutRemaining.HasValue ? (int)Math.Ceiling(lockoutRemaining.Value.TotalMinutes) : 15;
                ModelState.AddModelError(string.Empty, $"Account is locked. Please try again in {minutes} minute(s).");
                return View(model);

            case LoginResult.AccountInactive:
                await _audit.WriteAsync("Resident", null, "Login blocked - account inactive", cancellationToken);
                ModelState.AddModelError(string.Empty, "Your account has been deactivated. Please contact administration.");
                return View(model);

            case LoginResult.InvalidCredentials:
                _loginRateLimiter.RecordFailure("Resident", clientIp);
                await _audit.WriteAsync("Resident", null, "Login failed", cancellationToken);
                _logger.LogWarning("Resident login failed.");
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                return View(model);
        }

        await _audit.WriteAsync("Resident", resident!.Uid, "Login succeeded", cancellationToken);
        await HttpContext.SignInAsync(AuthSchemes.Resident, CreatePrincipal(resident));

        if (resident.MustChangePassword)
            return RedirectToAction("Index", "Password", new { area = "", mustChange = true });

        if (ReturnUrlGuard.IsSafe(returnUrl, adminPortal: false)
            && !returnUrl!.StartsWith("/login", StringComparison.OrdinalIgnoreCase))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Index), new { area = "" });
    }

    [Authorize(Policy = AuthPolicies.ResidentOnly)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId.HasValue)
        {
            var stampService = HttpContext.RequestServices.GetRequiredService<SecurityStampService>();
            await stampService.RotateStampAsync("Resident", userId.Value, cancellationToken);
        }

        await HttpContext.SignOutAsync(AuthSchemes.Resident);
        await _audit.WriteAsync("Resident", userId, "Logout", cancellationToken);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> NotFound(int? statusCode)
    {
        Response.StatusCode = statusCode ?? 404;
        ViewData["Title"] = "Page Not Found";

        if (await SchemeSignedInAsync(AuthSchemes.Admin, PortalRoles.Admin))
            ViewData["LayoutOverride"] = "~/Areas/AdminPortal/Views/Shared/_AdminLayout.cshtml";
        else if (await SchemeSignedInAsync(AuthSchemes.Receptionist, PortalRoles.Receptionist))
            ViewData["LayoutOverride"] = "~/Areas/ReceptionPortal/Views/Shared/_ReceptionLayout.cshtml";
        else if (await SchemeSignedInAsync(AuthSchemes.Resident, PortalRoles.Resident))
            ViewData["LayoutOverride"] = "~/Views/Shared/_ResidentLayout.cshtml";
        else
            ViewData["LayoutOverride"] = "~/Views/Shared/_StatusLayout.cshtml";

        return View();
    }

    private async Task<bool> SchemeSignedInAsync(string scheme, string role)
    {
        var result = await HttpContext.AuthenticateAsync(scheme);
        return result.Succeeded && result.Principal?.IsInRole(role) == true;
    }

    private async Task<bool> ResidentIsSignedInAsync()
    {
        var result = await HttpContext.AuthenticateAsync(AuthSchemes.Resident);
        return result.Succeeded
            && result.Principal?.IsInRole(PortalRoles.Resident) == true;
    }

    private static ClaimsPrincipal CreatePrincipal(Resident resident)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, resident.Uid.ToString()),
            new(ClaimTypes.Name, resident.ResidentName),
            new(ClaimTypes.Role, PortalRoles.Resident),
            new("username", resident.Username),
            new("SecurityStamp", resident.SecurityStamp ?? string.Empty)
        };

        if (!string.IsNullOrWhiteSpace(resident.Email))
            claims.Add(new Claim(ClaimTypes.Email, resident.Email));

        var identity = new ClaimsIdentity(claims, AuthSchemes.Resident);
        return new ClaimsPrincipal(identity);
    }
}
