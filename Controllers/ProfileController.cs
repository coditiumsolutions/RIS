using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResidentPortal.Data;
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
        var userId = User.GetUserId();
        if (userId is null)
        {
            await HttpContext.SignOutAsync(AuthSchemes.Resident);
            return RedirectToAction("Login", "Home");
        }

        var resident = await _db.Residents
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Uid == userId && r.IsActive, cancellationToken);

        if (resident is null)
        {
            await HttpContext.SignOutAsync(AuthSchemes.Resident);
            return RedirectToAction("Login", "Home");
        }

        return View(DashboardController.ToProfile(resident));
    }
}
