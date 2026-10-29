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
public class VisitorsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly AuditLogger _audit;
    private readonly AppSettingsService _settings;

    public VisitorsController(ApplicationDbContext db, AuditLogger audit, AppSettingsService settings)
    {
        _db = db;
        _audit = audit;
        _settings = settings;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? status, string? q, CancellationToken cancellationToken)
    {
        var query = _db.VisitorRequests.Include(v => v.Resident).AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(v => v.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var searchTerm = q.Trim().ToLower();
            query = query.Where(v =>
                v.VisitorName.ToLower().Contains(searchTerm) ||
                v.Phone.Contains(searchTerm) ||
                v.Resident.ResidentName.ToLower().Contains(searchTerm) ||
                (v.Resident.UnitNo != null && v.Resident.UnitNo.ToLower().Contains(searchTerm)));
        }

        var visitors = await query
            .OrderByDescending(v => v.Status == VisitorStatus.Pending ? 1 : 0)
            .ThenByDescending(v => v.CreatedAt)
            .Take(100)
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

        var today = HttpContext.RequestServices.GetRequiredService<BusinessTimeZone>().Today;
        var totalCount = await _db.VisitorRequests.CountAsync(cancellationToken);
        var pendingCount = await _db.VisitorRequests.CountAsync(v => v.Status == VisitorStatus.Pending, cancellationToken);
        var approvedTodayCount = await _db.VisitorRequests.CountAsync(v => v.ArrivalDate == today && v.Status == VisitorStatus.Approved, cancellationToken);
        var checkedInCount = await _db.VisitorRequests.CountAsync(v => v.Status == VisitorStatus.CheckedIn, cancellationToken);
        var approvalRequired = await _settings.IsVisitorApprovalRequiredAsync(cancellationToken);

        var model = new AdminVisitorListViewModel
        {
            Visitors = visitors,
            TotalCount = totalCount,
            PendingCount = pendingCount,
            ApprovedTodayCount = approvedTodayCount,
            CheckedInCount = checkedInCount,
            ApprovalRequired = approvalRequired,
            StatusFilter = status,
            SearchQuery = q
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var visitor = await _db.VisitorRequests
            .Include(v => v.Resident)
            .SingleOrDefaultAsync(v => v.Uid == id, cancellationToken);

        if (visitor is null)
            return NotFound();

        var model = new VisitorDetailsViewModel
        {
            Uid = visitor.Uid,
            VisitorName = visitor.VisitorName,
            Phone = visitor.Phone,
            Purpose = visitor.Purpose,
            Duration = visitor.Duration,
            ArrivalDate = visitor.ArrivalDate,
            NumberOfGuests = visitor.NumberOfGuests,
            CNIC = visitor.CNIC,
            VehicleRegistrationNo = visitor.VehicleRegistrationNo,
            Status = visitor.Status,
            IsWalkIn = visitor.IsWalkIn,
            CheckedInAt = visitor.CheckedInAt,
            CheckedOutAt = visitor.CheckedOutAt,
            CreatedAt = visitor.CreatedAt,
            ResidentName = visitor.Resident.ResidentName,
            ResidentUnit = visitor.Resident.UnitNo,
            ResidentMobile = visitor.Resident.Mobile,
            DeclineReason = visitor.DeclineReason
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, CancellationToken cancellationToken)
    {
        var visitor = await _db.VisitorRequests.FindAsync(new object[] { id }, cancellationToken);
        if (visitor is null)
            return NotFound();

        if (visitor.Status != VisitorStatus.Pending)
        {
            TempData["Error"] = "Only pending visitors can be approved.";
            return RedirectToAction(nameof(Index));
        }

        var userId = User.GetUserId();
        visitor.Status = VisitorStatus.Approved;
        visitor.ApprovedByUserId = userId;
        visitor.ApprovedAt = DateTime.UtcNow;
        visitor.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("Admin", userId, $"Approved visitor: {visitor.VisitorName} (ID: {visitor.Uid})", cancellationToken);

        TempData["Success"] = $"Visitor {visitor.VisitorName} has been approved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Decline(int id, CancellationToken cancellationToken)
    {
        var visitor = await _db.VisitorRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(v => v.Uid == id, cancellationToken);

        if (visitor is null)
            return NotFound();

        if (visitor.Status != VisitorStatus.Pending)
        {
            TempData["Error"] = "Only pending visitors can be declined.";
            return RedirectToAction(nameof(Index));
        }

        var model = new DeclineVisitorViewModel
        {
            Uid = visitor.Uid,
            VisitorName = visitor.VisitorName
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decline(DeclineVisitorViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var visitor = await _db.VisitorRequests.FindAsync(new object[] { model.Uid }, cancellationToken);
        if (visitor is null)
            return NotFound();

        if (visitor.Status != VisitorStatus.Pending)
        {
            TempData["Error"] = "Only pending visitors can be declined.";
            return RedirectToAction(nameof(Index));
        }

        var userId = User.GetUserId();
        visitor.Status = VisitorStatus.Declined;
        visitor.DeclinedByUserId = userId;
        visitor.DeclinedAt = DateTime.UtcNow;
        visitor.DeclineReason = model.Reason.Trim();
        visitor.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("Admin", userId, $"Declined visitor: {visitor.VisitorName} (ID: {visitor.Uid})", cancellationToken);

        TempData["Success"] = $"Visitor {visitor.VisitorName} has been declined.";
        return RedirectToAction(nameof(Index));
    }
}
