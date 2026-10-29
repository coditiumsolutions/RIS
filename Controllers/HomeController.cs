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
    private readonly ILogger<HomeController> _logger;

    public HomeController(PortalAuthentication authentication, AuditLogger audit, ILogger<HomeController> logger)
    {
        _authentication = authentication;
        _audit = audit;
        _logger = logger;
    }

    [Authorize(Policy = AuthPolicies.ResidentOnly)]
    [HttpGet]
    public IActionResult Index()
    {
        ViewData["Title"] = "Home";
        return View();
    }

    [AllowAnonymous]
    [HttpGet("/login")]
    public async Task<IActionResult> Login(string? returnUrl)
    {
        if (await ResidentIsSignedInAsync())
            return RedirectToAction(nameof(Index), new { area = "" });

        ViewData["ReturnUrl"] = returnUrl;
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

        var resident = await _authentication.ValidateResidentAsync(model.Username, model.Password, cancellationToken);
        if (resident is null)
        {
            await _audit.WriteAsync("Resident", null, "Login failed", cancellationToken);
            _logger.LogWarning("Resident login failed.");
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }

        await _audit.WriteAsync("Resident", resident.Uid, "Login succeeded", cancellationToken);
        await HttpContext.SignInAsync(AuthSchemes.Resident, CreatePrincipal(resident));

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
            new("username", resident.Username)
        };

        if (!string.IsNullOrWhiteSpace(resident.Email))
            claims.Add(new Claim(ClaimTypes.Email, resident.Email));

        var identity = new ClaimsIdentity(claims, AuthSchemes.Resident);
        return new ClaimsPrincipal(identity);
    }
}
