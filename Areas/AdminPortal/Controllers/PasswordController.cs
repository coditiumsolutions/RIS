using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResidentPortal.Data;
using ResidentPortal.Models;
using ResidentPortal.Security;
using ResidentPortal.Services;

namespace ResidentPortal.Areas.AdminPortal.Controllers;

[Area("AdminPortal")]
[Authorize(Policy = AuthPolicies.AdminOnly, AuthenticationSchemes = AuthSchemes.Admin)]
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
    public IActionResult Index()
    {
        return View(new ChangePasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ChangePasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var userId = User.GetUserId();
        if (userId is null)
        {
            await HttpContext.SignOutAsync(AuthSchemes.Admin);
            return Redirect("/AdminPortal");
        }

        var result = await _passwords.ChangeAdminPasswordAsync(
            userId.Value,
            model.CurrentPassword,
            model.NewPassword,
            cancellationToken);

        switch (result)
        {
            case PasswordChangeStatus.Success:
                await RefreshAdminCookieAsync(userId.Value, cancellationToken);
                TempData[ChangePasswordViewModel.SuccessMessageKey] = "Your password was changed.";
                return RedirectToAction("Index", "Dashboard", new { area = "AdminPortal" });
            case PasswordChangeStatus.CurrentPasswordIncorrect:
                ModelState.AddModelError(nameof(model.CurrentPassword), "The current password is incorrect.");
                return View(model);
            case PasswordChangeStatus.SameAsCurrent:
                ModelState.AddModelError(nameof(model.NewPassword), "New password must be different from the current password.");
                return View(model);
            default:
                await HttpContext.SignOutAsync(AuthSchemes.Admin);
                return Redirect("/AdminPortal");
        }
    }

    private async Task RefreshAdminCookieAsync(int userId, CancellationToken cancellationToken)
    {
        var admin = await _db.AdminUsers.AsNoTracking().SingleOrDefaultAsync(a => a.Uid == userId, cancellationToken);
        if (admin is null)
            return;

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

        await HttpContext.SignInAsync(AuthSchemes.Admin, new ClaimsPrincipal(new ClaimsIdentity(claims, AuthSchemes.Admin)));
    }
}
