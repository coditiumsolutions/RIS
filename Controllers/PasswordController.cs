using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResidentPortal.Models;
using ResidentPortal.Security;
using ResidentPortal.Services;

namespace ResidentPortal.Controllers;

[Authorize(Policy = AuthPolicies.ResidentOnly)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class PasswordController : Controller
{
    private readonly PasswordChangeService _passwords;

    public PasswordController(PasswordChangeService passwords)
    {
        _passwords = passwords;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View(new ChangePasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(
        [Bind(nameof(ChangePasswordViewModel.CurrentPassword), nameof(ChangePasswordViewModel.NewPassword), nameof(ChangePasswordViewModel.ConfirmPassword))]
        ChangePasswordViewModel model,
        CancellationToken cancellationToken)
    {
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
