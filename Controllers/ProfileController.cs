using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResidentPortal.Data;
using ResidentPortal.Models;
using ResidentPortal.Security;

namespace ResidentPortal.Controllers;

[Authorize(Policy = AuthPolicies.ResidentOnly)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ProfileController : Controller
{
    private readonly ApplicationDbContext _db;

    public ProfileController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var resident = await LoadActiveResidentAsync(cancellationToken);
        if (resident is null)
            return await SignOutToLoginAsync();

        ViewBag.EmailForm = new EditResidentEmailViewModel { Email = resident.Email };
        return View(DashboardController.ToProfile(resident));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateEmail(EditResidentEmailViewModel model, CancellationToken cancellationToken)
    {
        var resident = await _db.Residents.SingleOrDefaultAsync(r => r.Uid == User.GetUserId() && r.IsActive, cancellationToken);
        if (resident is null)
            return await SignOutToLoginAsync();

        if (!ModelState.IsValid)
        {
            ViewBag.EmailForm = model;
            return View(nameof(Index), DashboardController.ToProfile(resident));
        }

        resident.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
        await _db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Email updated.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<Resident?> LoadActiveResidentAsync(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return null;
        return await _db.Residents.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Uid == userId && r.IsActive, cancellationToken);
    }

    private async Task<IActionResult> SignOutToLoginAsync()
    {
        await HttpContext.SignOutAsync(AuthSchemes.Resident);
        return RedirectToAction("Login", "Home");
    }
}
