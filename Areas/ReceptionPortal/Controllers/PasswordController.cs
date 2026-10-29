using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResidentPortal.Data;
using ResidentPortal.Models;
using ResidentPortal.Security;
using ResidentPortal.Services;

namespace ResidentPortal.Areas.ReceptionPortal.Controllers;

[Area("ReceptionPortal")]
[Authorize(Policy = AuthPolicies.ReceptionistOnly)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class PasswordController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly PasswordChangeService _passwordChange;

    public PasswordController(ApplicationDbContext db, PasswordChangeService passwordChange)
    {
        _db = db;
        _passwordChange = passwordChange;
    }

    [HttpGet]
    public async Task<IActionResult> Index(bool mustChange, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return RedirectToAction("Index", "Account", new { area = "ReceptionPortal" });

        var receptionist = await _db.Receptionists
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Uid == userId, cancellationToken);

        if (receptionist is null)
            return RedirectToAction("Index", "Account", new { area = "ReceptionPortal" });

        ViewData["MustChange"] = mustChange || receptionist.MustChangePassword;
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
        if (userId is null)
            return RedirectToAction("Index", "Account", new { area = "ReceptionPortal" });

        var status = await _passwordChange.ChangeReceptionistPasswordAsync(
            userId.Value,
            model.CurrentPassword,
            model.NewPassword,
            cancellationToken);

        switch (status)
        {
            case PasswordChangeStatus.Success:
                await _db.SaveChangesAsync(cancellationToken);
                TempData[ChangePasswordViewModel.SuccessMessageKey] = "Your password has been changed successfully.";
                return RedirectToAction("Index", "Dashboard", new { area = "ReceptionPortal" });

            case PasswordChangeStatus.AccountUnavailable:
                return RedirectToAction("Index", "Account", new { area = "ReceptionPortal" });

            case PasswordChangeStatus.CurrentPasswordIncorrect:
                ModelState.AddModelError(nameof(model.CurrentPassword), "Current password is incorrect.");
                return View(model);

            case PasswordChangeStatus.SameAsCurrent:
                ModelState.AddModelError(nameof(model.NewPassword), "New password must be different from the current password.");
                return View(model);

            default:
                ModelState.AddModelError(string.Empty, "An error occurred while changing your password.");
                return View(model);
        }
    }
}
