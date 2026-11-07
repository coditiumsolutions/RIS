using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResidentPortal.Data;
using ResidentPortal.Models;
using ResidentPortal.Security;
using ResidentPortal.Services;

namespace ResidentPortal.Controllers;

[Authorize(Policy = AuthPolicies.ResidentOnly)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly BusinessTimeZone _timeZone;

    public DashboardController(ApplicationDbContext db, BusinessTimeZone timeZone)
    {
        _db = db;
        _timeZone = timeZone;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var resident = await FindCurrentResidentAsync(cancellationToken);
        if (resident is null)
            return await SignOutInactiveAsync();

        var today = _timeZone.Today;
        var recentVisitors = (await _db.VisitorRequests
            .AsNoTracking()
            .Where(v => v.ResidentId == resident.Uid)
            .OrderByDescending(v => v.CreatedAt)
            .Take(5)
            .Select(v => new
            {
                v.VisitorName,
                v.Purpose,
                v.Status,
                v.CreatedAt,
                v.ArrivalDate
            })
            .ToListAsync(cancellationToken))
            .Select(v => new VisitorActivityItem
            {
                VisitorName = v.VisitorName,
                Purpose = v.Purpose,
                Status = VisitorStatus.Effective(v.Status, v.ArrivalDate, today),
                When = v.CreatedAt
            })
            .ToList();

        var model = new ResidentDashboardViewModel
        {
            Profile = ToProfile(resident),
            RecentVisitors = recentVisitors
        };
        return View(model);
    }

    private async Task<Resident?> FindCurrentResidentAsync(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return null;

        return await _db.Residents
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Uid == userId && r.IsActive, cancellationToken);
    }

    internal static ResidentProfileViewModel ToProfile(Resident resident) => new()
    {
        ResidentName = resident.ResidentName,
        Username = resident.Username,
        CustomerId = resident.CustomerId,
        Email = resident.Email ?? "Not provided",
        Mobile = resident.Mobile ?? "Not provided",
        UnitNo = resident.UnitNo,
        Street = resident.Street,
        Tower = resident.Tower,
        Floor = resident.Floor,
        Block = resident.Block,
        IsActive = resident.IsActive,
        CreatedAt = resident.CreatedAt
    };

    private async Task<IActionResult> SignOutInactiveAsync()
    {
        await HttpContext.SignOutAsync(AuthSchemes.Resident);
        return RedirectToAction("Login", "Home");
    }
}
