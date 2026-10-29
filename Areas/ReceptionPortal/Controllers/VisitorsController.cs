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
    public async Task<IActionResult> Index(string? q, DateOnly? date, CancellationToken cancellationToken)
    {
        var today = date ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var query = _db.VisitorRequests
            .Include(v => v.Resident)
            .Where(v => v.Status == VisitorStatus.Approved ||
                        v.Status == VisitorStatus.CheckedIn ||
                        (v.ArrivalDate == today && v.Status == VisitorStatus.Pending));

        if (!string.IsNullOrWhiteSpace(q))
        {
            var searchTerm = q.Trim().ToLower();
            query = query.Where(v =>
                v.VisitorName.ToLower().Contains(searchTerm) ||
                v.Phone.Contains(searchTerm) ||
                v.Resident.ResidentName.ToLower().Contains(searchTerm) ||
                (v.Resident.UnitNo != null && v.Resident.UnitNo.ToLower().Contains(searchTerm)));
        }

        if (date.HasValue)
        {
            query = query.Where(v => v.ArrivalDate == date.Value);
        }

        var visitors = await query
            .OrderByDescending(v => v.ArrivalDate == today ? 1 : 0)
            .ThenBy(v => v.Status == VisitorStatus.CheckedIn ? 0 : 1)
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

        var expectedTodayCount = await _db.VisitorRequests
            .CountAsync(v => v.ArrivalDate == today &&
                (v.Status == VisitorStatus.Approved || v.Status == VisitorStatus.Pending), cancellationToken);

        var checkedInCount = await _db.VisitorRequests
            .CountAsync(v => v.Status == VisitorStatus.CheckedIn, cancellationToken);

        var checkedOutTodayCount = await _db.VisitorRequests
            .CountAsync(v => v.CheckedOutAt != null && v.CheckedOutAt.Value.Date == DateTime.UtcNow.Date, cancellationToken);

        var model = new ReceptionistVisitorListViewModel
        {
            Visitors = visitors,
            ExpectedTodayCount = expectedTodayCount,
            CheckedInCount = checkedInCount,
            CheckedOutTodayCount = checkedOutTodayCount,
            SearchQuery = q,
            DateFilter = date
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
    public async Task<IActionResult> CheckIn(int id, CancellationToken cancellationToken)
    {
        var visitor = await _db.VisitorRequests.FindAsync(new object[] { id }, cancellationToken);
        if (visitor is null)
            return NotFound();

        if (visitor.Status != VisitorStatus.Approved && visitor.Status != VisitorStatus.Pending)
        {
            TempData["Error"] = "Only approved or pending visitors can be checked in.";
            return RedirectToAction(nameof(Index));
        }

        var userId = User.GetUserId();
        visitor.Status = VisitorStatus.CheckedIn;
        visitor.CheckedInAt = DateTime.UtcNow;
        visitor.CheckedInByUserId = userId;
        visitor.CheckedInByUserType = "Receptionist";
        visitor.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("Receptionist", userId, $"Checked in visitor: {visitor.VisitorName} (ID: {visitor.Uid})", cancellationToken);

        TempData["Success"] = $"{visitor.VisitorName} has been checked in.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckOut(int id, CancellationToken cancellationToken)
    {
        var visitor = await _db.VisitorRequests.FindAsync(new object[] { id }, cancellationToken);
        if (visitor is null)
            return NotFound();

        if (visitor.Status != VisitorStatus.CheckedIn)
        {
            TempData["Error"] = "Only checked-in visitors can be checked out.";
            return RedirectToAction(nameof(Index));
        }

        var userId = User.GetUserId();
        visitor.Status = VisitorStatus.CheckedOut;
        visitor.CheckedOutAt = DateTime.UtcNow;
        visitor.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("Receptionist", userId, $"Checked out visitor: {visitor.VisitorName} (ID: {visitor.Uid})", cancellationToken);

        TempData["Success"] = $"{visitor.VisitorName} has been checked out.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> WalkIn(CancellationToken cancellationToken)
    {
        var residents = await _db.Residents
            .Where(r => r.IsActive)
            .OrderBy(r => r.ResidentName)
            .Select(r => new ResidentSelectItem
            {
                Uid = r.Uid,
                Name = r.ResidentName,
                UnitNo = r.UnitNo
            })
            .ToListAsync(cancellationToken);

        var model = new WalkInVisitorViewModel
        {
            AvailableResidents = residents,
            Duration = VisitorDuration.LessThan6Hours
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WalkIn(WalkInVisitorViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.AvailableResidents = await GetResidentSelectItemsAsync(cancellationToken);
            return View(model);
        }

        var resident = await _db.Residents.FindAsync(new object[] { model.ResidentId }, cancellationToken);
        if (resident is null || !resident.IsActive)
        {
            ModelState.AddModelError(nameof(model.ResidentId), "Selected resident not found or is inactive.");
            model.AvailableResidents = await GetResidentSelectItemsAsync(cancellationToken);
            return View(model);
        }

        var maxGuests = await _settings.GetMaxGuestsPerVisitAsync(cancellationToken);
        if (model.NumberOfGuests > maxGuests)
        {
            ModelState.AddModelError(nameof(model.NumberOfGuests), $"Maximum {maxGuests} guests allowed per visit.");
            model.AvailableResidents = await GetResidentSelectItemsAsync(cancellationToken);
            return View(model);
        }

        var userId = User.GetUserId();
        var now = DateTime.UtcNow;

        var visitor = new VisitorRequest
        {
            ResidentId = model.ResidentId,
            VisitorName = model.VisitorName.Trim(),
            Phone = model.Phone.Trim(),
            Purpose = model.Purpose.Trim(),
            Duration = model.Duration,
            ArrivalDate = DateOnly.FromDateTime(now),
            NumberOfGuests = model.NumberOfGuests,
            CNIC = model.CNIC?.Trim(),
            VehicleRegistrationNo = model.VehicleRegistrationNo?.Trim(),
            Status = VisitorStatus.CheckedIn,
            IsWalkIn = true,
            CheckedInAt = now,
            CheckedInByUserId = userId,
            CheckedInByUserType = "Receptionist",
            ApprovedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.VisitorRequests.Add(visitor);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("Receptionist", userId, $"Registered walk-in visitor: {visitor.VisitorName} for resident {resident.ResidentName}", cancellationToken);

        TempData["Success"] = $"Walk-in visitor {visitor.VisitorName} has been registered and checked in.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IReadOnlyList<ResidentSelectItem>> GetResidentSelectItemsAsync(CancellationToken cancellationToken)
    {
        return await _db.Residents
            .Where(r => r.IsActive)
            .OrderBy(r => r.ResidentName)
            .Select(r => new ResidentSelectItem
            {
                Uid = r.Uid,
                Name = r.ResidentName,
                UnitNo = r.UnitNo
            })
            .ToListAsync(cancellationToken);
    }
}
