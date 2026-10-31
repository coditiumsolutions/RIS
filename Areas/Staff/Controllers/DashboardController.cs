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
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _db;

    public DashboardController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        // Receptionists land on the desk; admin dashboard is for Admin / management roles.
        if (string.Equals(User.GetStaffUserType(), PortalRoles.Receptionist, StringComparison.OrdinalIgnoreCase)
            && !User.HasPermission(StaffPermissions.ResidentsManage)
            && !User.HasPermission(StaffPermissions.StaffManage)
            && !User.HasPermission(StaffPermissions.SettingsManage))
        {
            return RedirectToAction("Index", "Desk", new { area = "Staff" });
        }

        if (!User.HasPermission(StaffPermissions.ResidentsManage)
            && !User.IsInRole(PortalRoles.Admin)
            && !User.HasPermission(StaffPermissions.StaffManage)
            && !User.HasPermission(StaffPermissions.SettingsManage))
        {
            return RedirectToAction("Index", "Desk", new { area = "Staff" });
        }

        var userId = User.GetUserId();
        if (userId is null)
            return await SignOutInactiveAsync();

        string displayName;
        string username;
        var userType = User.GetStaffUserType();

        if (string.Equals(userType, PortalRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            var admin = await _db.AdminUsers
                .AsNoTracking()
                .SingleOrDefaultAsync(a => a.Uid == userId && a.IsActive, cancellationToken);
            if (admin is null)
                return await SignOutInactiveAsync();
            displayName = admin.FullName;
            username = admin.Username;
        }
        else if (string.Equals(userType, PortalRoles.Receptionist, StringComparison.OrdinalIgnoreCase))
        {
            var receptionist = await _db.Receptionists
                .AsNoTracking()
                .SingleOrDefaultAsync(r => r.Uid == userId && r.IsActive, cancellationToken);
            if (receptionist is null)
                return await SignOutInactiveAsync();
            displayName = receptionist.FullName;
            username = receptionist.Username;
        }
        else
        {
            return await SignOutInactiveAsync();
        }

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
            AdminName = displayName,
            Username = username,
            ResidentCount = residentCount,
            VisitorsTodayCount = visitorsToday,
            PortalUserCount = portalUserCount,
            RecentActivity = recentActivity
        };
        return View(model);
    }

    private async Task<IActionResult> SignOutInactiveAsync()
    {
        await HttpContext.SignOutAsync(AuthSchemes.Staff);
        return RedirectToAction("Index", "Account", new { area = "Staff" });
    }
}
