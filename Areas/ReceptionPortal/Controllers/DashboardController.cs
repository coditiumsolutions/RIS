using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResidentPortal.Data;
using ResidentPortal.Models;
using ResidentPortal.Security;

namespace ResidentPortal.Areas.ReceptionPortal.Controllers;

[Area("ReceptionPortal")]
[Authorize(Policy = AuthPolicies.ReceptionistOnly)]
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

        var receptionist = await _db.Receptionists
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Uid == userId && r.IsActive, cancellationToken);

        if (receptionist is null)
            return await SignOutInactiveAsync();

        if (receptionist.MustChangePassword)
            return RedirectToAction("Index", "Password", new { area = "ReceptionPortal", mustChange = true });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var expectedToday = await _db.VisitorRequests
            .CountAsync(v => v.ArrivalDate == today &&
                (v.Status == VisitorStatus.Approved || v.Status == VisitorStatus.Pending), cancellationToken);

        var checkedInToday = await _db.VisitorRequests
            .CountAsync(v => v.CheckedInAt != null &&
                v.CheckedInAt.Value.Date == DateTime.UtcNow.Date &&
                v.Status == VisitorStatus.CheckedIn, cancellationToken);

        var checkedOutToday = await _db.VisitorRequests
            .CountAsync(v => v.CheckedOutAt != null &&
                v.CheckedOutAt.Value.Date == DateTime.UtcNow.Date, cancellationToken);

        var walkInsToday = await _db.VisitorRequests
            .CountAsync(v => v.IsWalkIn && v.CreatedAt.Date == DateTime.UtcNow.Date, cancellationToken);

        var recentActivity = await _db.VisitorRequests
            .Include(v => v.Resident)
            .Where(v => v.ArrivalDate >= today.AddDays(-1) || v.CheckedInAt != null || v.CheckedOutAt != null)
            .OrderByDescending(v => v.UpdatedAt)
            .Take(10)
            .Select(v => new VisitorRequestListItem
            {
                Uid = v.Uid,
                VisitorName = v.VisitorName,
                Phone = v.Phone,
                Purpose = v.Purpose,
                Duration = v.Duration,
                ArrivalDate = v.ArrivalDate,
                NumberOfGuests = v.NumberOfGuests,
                Status = v.Status,
                IsWalkIn = v.IsWalkIn,
                CheckedInAt = v.CheckedInAt,
                CheckedOutAt = v.CheckedOutAt,
                CreatedAt = v.CreatedAt,
                ResidentName = v.Resident.ResidentName,
                ResidentUnit = v.Resident.UnitNo
            })
            .ToListAsync(cancellationToken);

        var model = new ReceptionistDashboardViewModel
        {
            ReceptionistName = receptionist.FullName,
            Username = receptionist.Username,
            ExpectedVisitorsToday = expectedToday,
            CheckedInToday = checkedInToday,
            CheckedOutToday = checkedOutToday,
            WalkInsToday = walkInsToday,
            RecentActivity = recentActivity
        };

        return View(model);
    }

    private async Task<IActionResult> SignOutInactiveAsync()
    {
        await HttpContext.SignOutAsync(AuthSchemes.Receptionist);
        return RedirectToAction("Index", "Account", new { area = "ReceptionPortal" });
    }
}
