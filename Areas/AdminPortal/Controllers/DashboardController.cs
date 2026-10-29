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
[Authorize(Policy = AuthPolicies.AdminOnly)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _db;

    public DashboardController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return await SignOutInactiveAsync();

        var admin = await _db.AdminUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.Uid == userId && a.IsActive, cancellationToken);

        if (admin is null)
            return await SignOutInactiveAsync();

        var model = new AdminDashboardViewModel
        {
            AdminName = admin.FullName,
            Username = admin.Username,
            SampleResidentCount = 128,
            SampleVisitorCount = 14,
            SampleUserCount = 6,
            RecentActivity = SampleDashboardData.RecentActivity
        };
        return View(model);
    }

    private async Task<IActionResult> SignOutInactiveAsync()
    {
        await HttpContext.SignOutAsync(AuthSchemes.Admin);
        return RedirectToAction("Index", "Account", new { area = "AdminPortal" });
    }
}
