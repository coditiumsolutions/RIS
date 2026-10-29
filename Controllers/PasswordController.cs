using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResidentPortal.Data;
using ResidentPortal.Models;
using ResidentPortal.Security;
using ResidentPortal.Services;

namespace ResidentPortal.Controllers;

[Authorize(Policy = AuthPolicies.ResidentOnly)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class PasswordController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly PasswordChangeService _passwords;

    public PasswordController(ApplicationDbContext db, PasswordChangeService passwords)
    {
        _db = db;
        _passwords = passwords;
    }

    [HttpGet]
    public async Task<IActionResult> Index(bool mustChange, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return RedirectToAction("Login", "Home", new { area = "" });

        var resident = await _db.Residents
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Uid == userId, cancellationToken);

        if (resident is null)
            return RedirectToAction("Login", "Home", new { area = "" });

        ViewData["MustChange"] = mustChange || resident.MustChangePassword;
        return View(new ChangePasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(
        [Bind(nameof(ChangePasswordViewModel.CurrentPassword), nameof(ChangePasswordViewModel.NewPassword), nameof(ChangePasswordViewModel.ConfirmPassword))]
        ChangePasswordViewModel model,
        bool mustChange,
        CancellationToken cancellationToken)
    {
        ViewData["MustChange"] = mustChange;
        
        if (!ModelState.IsValid)
            return View(model);

        var userId = User.GetUserId();
        if (userId is null)
        {
            await HttpContext.SignOutAsync(AuthSchemes.Resident);
            return RedirectToAction("Login", "Home", new { area = "" });
        }

        var result = await _passwords.ChangeResidentPasswordAsync(
            userId.Value,
            model.CurrentPassword,
            model.NewPassword,
            cancellationToken);

        switch (result)
        {
            case PasswordChangeStatus.Success:
                var resident = await _db.Residents.AsNoTracking().SingleOrDefaultAsync(r => r.Uid == userId.Value, cancellationToken);
                if (resident is not null)
                {
                    var claims = new List<System.Security.Claims.Claim>
                    {
                        new(System.Security.Claims.ClaimTypes.NameIdentifier, resident.Uid.ToString()),
                        new(System.Security.Claims.ClaimTypes.Name, resident.ResidentName),
                        new(System.Security.Claims.ClaimTypes.Role, PortalRoles.Resident),
                        new("username", resident.Username),
                        new("SecurityStamp", resident.SecurityStamp ?? string.Empty)
                    };
                    if (!string.IsNullOrWhiteSpace(resident.Email))
                        claims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, resident.Email));
                    await HttpContext.SignInAsync(AuthSchemes.Resident, new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(claims, AuthSchemes.Resident)));
                }
                TempData[ChangePasswordViewModel.SuccessMessageKey] = "Your password was changed.";
                return RedirectToAction("Index", "Dashboard", new { area = "" });
            case PasswordChangeStatus.CurrentPasswordIncorrect:
                ModelState.AddModelError(nameof(model.CurrentPassword), "The current password is incorrect.");
                return View(model);
            case PasswordChangeStatus.SameAsCurrent:
                ModelState.AddModelError(nameof(model.NewPassword), "New password must be different from the current password.");
                return View(model);
            default:
                await HttpContext.SignOutAsync(AuthSchemes.Resident);
                return RedirectToAction("Login", "Home", new { area = "" });
        }
    }
}
