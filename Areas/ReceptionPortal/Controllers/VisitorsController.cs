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
    private readonly BusinessTimeZone _timeZone;
    private readonly VisitorPhotoService _photos;

    public VisitorsController(
        ApplicationDbContext db,
        AuditLogger audit,
        AppSettingsService settings,
        BusinessTimeZone timeZone,
        VisitorPhotoService photos)
    {
        _db = db;
        _audit = audit;
        _settings = settings;
        _timeZone = timeZone;
        _photos = photos;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q, DateOnly? date, CancellationToken cancellationToken)
    {
        var today = date ?? _timeZone.Today;

        var query = _db.VisitorRequests
            .Include(v => v.Resident)
            .Where(v => v.Status == VisitorStatus.Approved ||
                        v.Status == VisitorStatus.CheckedIn ||
                        v.Status == VisitorStatus.CheckedOut ||
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

        var todayStartUtc = _timeZone.ConvertToUtc(today.ToDateTime(TimeOnly.MinValue));
        var tomorrowStartUtc = _timeZone.ConvertToUtc(today.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var checkedOutTodayCount = await _db.VisitorRequests
            .CountAsync(v => v.CheckedOutAt != null &&
                             v.CheckedOutAt.Value >= todayStartUtc &&
                             v.CheckedOutAt.Value < tomorrowStartUtc, cancellationToken);

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

        var photos = await _db.VisitorPhotos
            .AsNoTracking()
            .Where(p => p.VisitorRequestId == visitor.Uid)
            .OrderBy(p => p.CapturedAtUtc)
            .Select(p => new VisitorPhotoItemViewModel
            {
                Uid = p.Uid,
                Kind = p.Kind,
                CapturedAtUtc = p.CapturedAtUtc
            })
            .ToListAsync(cancellationToken);

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
            DeclineReason = visitor.DeclineReason,
            CanCheckIn = visitor.Status == VisitorStatus.Approved
                && VisitorDuration.IsWithinVisitWindow(visitor.ArrivalDate, visitor.Duration, _timeZone.Today),
            Photos = photos
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> CheckIn(int id, string? q, DateOnly? date, IFormFile? photo, CancellationToken cancellationToken)
    {
        var visitor = await _db.VisitorRequests.FindAsync(new object[] { id }, cancellationToken);
        if (visitor is null)
            return NotFound();

        if (visitor.Status != VisitorStatus.Approved)
        {
            TempData["Error"] = "Only approved visitors can be checked in.";
            return RedirectToAction(nameof(Index), new { q, date });
        }

        var today = _timeZone.Today;
        if (!VisitorDuration.IsWithinVisitWindow(visitor.ArrivalDate, visitor.Duration, today))
        {
            TempData["Error"] = "This visitor can only be checked in on the arrival date through the end of the visit duration.";
            return RedirectToAction(nameof(Index), new { q, date });
        }

        var userId = User.GetUserId();
        visitor.Status = VisitorStatus.CheckedIn;
        visitor.CheckedInAt = DateTime.UtcNow;
        visitor.CheckedInByUserId = userId;
        visitor.CheckedInByUserType = "Receptionist";
        visitor.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("Receptionist", userId, $"Checked in visitor: {visitor.VisitorName} (ID: {visitor.Uid})", cancellationToken);

        var (_, photoWarning) = await _photos.TryAttachAsync(
            visitor.Uid, VisitorPhotoKind.CheckIn, photo, userId, null, cancellationToken);

        TempData["Success"] = string.IsNullOrEmpty(photoWarning)
            ? $"{visitor.VisitorName} has been checked in."
            : $"{visitor.VisitorName} has been checked in. Photo was skipped: {photoWarning}";
        return RedirectToAction(nameof(Index), new { q, date });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> CheckOut(int id, string? q, DateOnly? date, IFormFile? photo, CancellationToken cancellationToken)
    {
        var visitor = await _db.VisitorRequests.FindAsync(new object[] { id }, cancellationToken);
        if (visitor is null)
            return NotFound();

        if (visitor.Status != VisitorStatus.CheckedIn)
        {
            TempData["Error"] = "Only checked-in visitors can be checked out.";
            return RedirectToAction(nameof(Index), new { q, date });
        }

        var userId = User.GetUserId();
        visitor.Status = VisitorStatus.CheckedOut;
        visitor.CheckedOutAt = DateTime.UtcNow;
        visitor.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("Receptionist", userId, $"Checked out visitor: {visitor.VisitorName} (ID: {visitor.Uid})", cancellationToken);

        var (_, photoWarning) = await _photos.TryAttachAsync(
            visitor.Uid, VisitorPhotoKind.CheckOut, photo, userId, null, cancellationToken);

        TempData["Success"] = string.IsNullOrEmpty(photoWarning)
            ? $"{visitor.VisitorName} has been checked out."
            : $"{visitor.VisitorName} has been checked out. Photo was skipped: {photoWarning}";
        return RedirectToAction(nameof(Index), new { q, date });
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
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> WalkIn(WalkInVisitorViewModel model, IFormFile? photo, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.AvailableResidents = await GetResidentSelectItemsAsync(cancellationToken);
            return View(model);
        }

        if (!VisitorDuration.IsAllowed(model.Duration))
        {
            ModelState.AddModelError(nameof(model.Duration), "Please select a valid visit duration.");
            model.AvailableResidents = await GetResidentSelectItemsAsync(cancellationToken);
            return View(model);
        }

        var resident = await _db.Residents.FindAsync(new object[] { model.ResidentId!.Value }, cancellationToken);
        if (resident is null || !resident.IsActive)
        {
            ModelState.AddModelError(nameof(model.ResidentId), "Selected resident not found or is inactive.");
            model.AvailableResidents = await GetResidentSelectItemsAsync(cancellationToken);
            return View(model);
        }

        var maxGuests = await _settings.GetMaxGuestsPerVisitAsync(cancellationToken);
        if (!model.NumberOfGuests.HasValue || model.NumberOfGuests.Value < 1 || model.NumberOfGuests.Value > maxGuests)
        {
            ModelState.AddModelError(nameof(model.NumberOfGuests), $"Number of guests must be between 1 and {maxGuests}.");
            model.AvailableResidents = await GetResidentSelectItemsAsync(cancellationToken);
            return View(model);
        }

        var userId = User.GetUserId();
        var now = DateTime.UtcNow;

        var visitor = new VisitorRequest
        {
            ResidentId = model.ResidentId.Value,
            VisitorName = model.VisitorName.Trim(),
            Phone = model.Phone.Trim(),
            Purpose = model.Purpose.Trim(),
            Duration = model.Duration,
            ArrivalDate = _timeZone.Today,
            NumberOfGuests = model.NumberOfGuests.Value,
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

        var (_, photoWarning) = await _photos.TryAttachAsync(
            visitor.Uid, VisitorPhotoKind.CheckIn, photo, userId, null, cancellationToken);

        TempData["Success"] = string.IsNullOrEmpty(photoWarning)
            ? $"Walk-in visitor {visitor.VisitorName} has been registered and checked in."
            : $"Walk-in visitor {visitor.VisitorName} has been registered and checked in. Photo was skipped: {photoWarning}";
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
