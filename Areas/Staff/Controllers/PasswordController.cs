using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResidentPortal.Data;
using ResidentPortal.Models;
using ResidentPortal.Security;
using ResidentPortal.Services;

namespace ResidentPortal.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Policy = AuthPolicies.StaffOnly, AuthenticationSchemes = AuthSchemes.Staff)]
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
        var userType = User.GetStaffUserType();
        if (userId is null || string.IsNullOrWhiteSpace(userType))
            return await SignOutToLoginAsync();

        var flag = mustChange;
        if (string.Equals(userType, PortalRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            var admin = await _db.AdminUsers.AsNoTracking()
                .SingleOrDefaultAsync(a => a.Uid == userId, cancellationToken);
            if (admin is null)
                return await SignOutToLoginAsync();
            flag = mustChange || admin.MustChangePassword;
        }
        else if (string.Equals(userType, PortalRoles.Receptionist, StringComparison.OrdinalIgnoreCase))
        {
            var receptionist = await _db.Receptionists.AsNoTracking()
                .SingleOrDefaultAsync(r => r.Uid == userId, cancellationToken);
            if (receptionist is null)
                return await SignOutToLoginAsync();
            flag = mustChange || receptionist.MustChangePassword;
        }
        else
        {
            return await SignOutToLoginAsync();
        }

        ViewData["MustChange"] = flag;
        return View(new ChangePasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ChangePasswordViewModel model, bool mustChange, CancellationToken cancellationToken)
    {
        ViewData["MustChange"] = mustChange;

        if (!ModelState.IsValid)
            return View(model);

        var userId = User.GetUserId();
        var userType = User.GetStaffUserType();
        if (userId is null || string.IsNullOrWhiteSpace(userType))
            return await SignOutToLoginAsync();

        PasswordChangeStatus result;
        if (string.Equals(userType, PortalRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            result = await _passwords.ChangeAdminPasswordAsync(
                userId.Value, model.CurrentPassword, model.NewPassword, cancellationToken);
        }
        else if (string.Equals(userType, PortalRoles.Receptionist, StringComparison.OrdinalIgnoreCase))
        {
            result = await _passwords.ChangeReceptionistPasswordAsync(
                userId.Value, model.CurrentPassword, model.NewPassword, cancellationToken);
        }
        else
        {
            return await SignOutToLoginAsync();
        }

        switch (result)
        {
            case PasswordChangeStatus.Success:
                await RefreshStaffCookieAsync(userId.Value, userType, cancellationToken);
                TempData[ChangePasswordViewModel.SuccessMessageKey] = "Your password was changed.";
                return string.Equals(userType, PortalRoles.Receptionist, StringComparison.OrdinalIgnoreCase)
                    ? RedirectToAction("Index", "Desk", new { area = "Staff" })
                    : RedirectToAction("Index", "Dashboard", new { area = "Staff" });

            case PasswordChangeStatus.CurrentPasswordIncorrect:
                ModelState.AddModelError(nameof(model.CurrentPassword), "The current password is incorrect.");
                return View(model);

            case PasswordChangeStatus.SameAsCurrent:
                ModelState.AddModelError(nameof(model.NewPassword), "New password must be different from the current password.");
                return View(model);

            case PasswordChangeStatus.AccountUnavailable:
                return await SignOutToLoginAsync();

            default:
                ModelState.AddModelError(string.Empty, "An error occurred while changing your password.");
                return View(model);
        }
    }

    private async Task RefreshStaffCookieAsync(int userId, string userType, CancellationToken cancellationToken)
    {
        if (string.Equals(userType, PortalRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            var admin = await _db.AdminUsers.AsNoTracking()
                .SingleOrDefaultAsync(a => a.Uid == userId, cancellationToken);
            if (admin is null)
                return;
            await HttpContext.SignInAsync(AuthSchemes.Staff, StaffPrincipalFactory.Create(admin));
            return;
        }

        if (string.Equals(userType, PortalRoles.Receptionist, StringComparison.OrdinalIgnoreCase))
        {
            var receptionist = await _db.Receptionists.AsNoTracking()
                .SingleOrDefaultAsync(r => r.Uid == userId, cancellationToken);
            if (receptionist is null)
                return;
            await HttpContext.SignInAsync(AuthSchemes.Staff, StaffPrincipalFactory.Create(receptionist));
        }
    }

    private async Task<IActionResult> SignOutToLoginAsync()
    {
        await HttpContext.SignOutAsync(AuthSchemes.Staff);
        return Redirect("/Staff");
    }
}
