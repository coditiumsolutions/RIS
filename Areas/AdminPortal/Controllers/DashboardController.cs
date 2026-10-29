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

        var timeZone = HttpContext.RequestServices.GetRequiredService<BusinessTimeZone>();
        var today = timeZone.Today;

        var residentCount = await _db.Residents.CountAsync(cancellationToken);
        var visitorsToday = await _db.VisitorRequests.CountAsync(v => v.ArrivalDate == today, cancellationToken);
        var portalUserCount = await _db.AdminUsers.CountAsync(cancellationToken)
            + await _db.Receptionists.CountAsync(cancellationToken);

        var recentActivity = await _db.AuditLogs
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .Take(8)
            .Select(a => new AdminActivityItem
            {
                Title = a.Action,
                Detail = a.UserType + (a.UserId.HasValue ? $" #{a.UserId}" : string.Empty),
                When = a.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var model = new AdminDashboardViewModel
        {
            AdminName = admin.FullName,
            Username = admin.Username,
            ResidentCount = residentCount,
            VisitorsTodayCount = visitorsToday,
            PortalUserCount = portalUserCount,
            RecentActivity = recentActivity
        };
        return View(model);
    }

    private async Task<IActionResult> SignOutInactiveAsync()
    {
        await HttpContext.SignOutAsync(AuthSchemes.Admin);
        return RedirectToAction("Index", "Account", new { area = "AdminPortal" });
    }
}
