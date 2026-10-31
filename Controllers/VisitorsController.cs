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
public class VisitorsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly AuditLogger _audit;
    private readonly AppSettingsService _settings;
    private readonly BusinessTimeZone _timeZone;

    public VisitorsController(ApplicationDbContext db, AuditLogger audit, AppSettingsService settings, BusinessTimeZone timeZone)
    {
        _db = db;
        _audit = audit;
        _settings = settings;
        _timeZone = timeZone;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return RedirectToAction("Login", "Home");

        if (!await ResidentIsActiveAsync(userId.Value, cancellationToken))
            return await SignOutInactiveAsync();

        var visitors = await _db.VisitorRequests
            .Where(v => v.ResidentId == userId.Value)
            .OrderByDescending(v => v.ArrivalDate)
            .ThenByDescending(v => v.CreatedAt)
            .Take(50)
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
                CreatedAt = v.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var totalCount = await _db.VisitorRequests.CountAsync(v => v.ResidentId == userId.Value, cancellationToken);
        var pendingCount = await _db.VisitorRequests.CountAsync(v => v.ResidentId == userId.Value && v.Status == VisitorStatus.Pending, cancellationToken);
        var approvedCount = await _db.VisitorRequests.CountAsync(v => v.ResidentId == userId.Value && v.Status == VisitorStatus.Approved, cancellationToken);
        var checkedInCount = await _db.VisitorRequests.CountAsync(v => v.ResidentId == userId.Value && v.Status == VisitorStatus.CheckedIn, cancellationToken);

        var model = new ResidentVisitorListViewModel
        {
            Visitors = visitors,
            TotalCount = totalCount,
            PendingCount = pendingCount,
            ApprovedCount = approvedCount,
            CheckedInCount = checkedInCount
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Register(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return RedirectToAction("Login", "Home");

        if (!await ResidentIsActiveAsync(userId.Value, cancellationToken))
            return await SignOutInactiveAsync();

        var model = new RegisterVisitorViewModel
        {
            ArrivalDate = _timeZone.Today,
            Duration = VisitorDuration.LessThan6Hours
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterVisitorViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var userId = User.GetUserId();
        if (userId is null)
            return RedirectToAction("Login", "Home");

        if (!await ResidentIsActiveAsync(userId.Value, cancellationToken))
            return await SignOutInactiveAsync();

        var maxGuests = await _settings.GetMaxGuestsPerVisitAsync(cancellationToken);
        if (!model.NumberOfGuests.HasValue || model.NumberOfGuests.Value < 1 || model.NumberOfGuests.Value > maxGuests)
        {
            ModelState.AddModelError(nameof(model.NumberOfGuests), $"Number of guests must be between 1 and {maxGuests}.");
            return View(model);
        }

        if (!VisitorDuration.IsAllowed(model.Duration))
        {
            ModelState.AddModelError(nameof(model.Duration), "Please select a valid visit duration.");
            return View(model);
        }

        if (!model.ArrivalDate.HasValue)
        {
            ModelState.AddModelError(nameof(model.ArrivalDate), "Arrival date is required.");
            return View(model);
        }

        if (model.ArrivalDate.Value < _timeZone.Today)
        {
            ModelState.AddModelError(nameof(model.ArrivalDate), "Arrival date cannot be in the past.");
            return View(model);
        }

        var approvalRequired = await _settings.IsVisitorApprovalRequiredAsync(cancellationToken);
        var now = DateTime.UtcNow;

        var visitor = new VisitorRequest
        {
            ResidentId = userId.Value,
            VisitorName = model.VisitorName.Trim(),
            Phone = model.Phone.Trim(),
            Purpose = model.Purpose.Trim(),
            Duration = model.Duration,
            ArrivalDate = model.ArrivalDate.Value,
            NumberOfGuests = model.NumberOfGuests!.Value,
            CNIC = model.CNIC?.Trim(),
            VehicleRegistrationNo = model.VehicleRegistrationNo?.Trim(),
            Status = approvalRequired ? VisitorStatus.Pending : VisitorStatus.Approved,
            IsWalkIn = false,
            ApprovedAt = approvalRequired ? null : now,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.VisitorRequests.Add(visitor);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("Resident", userId, $"Registered visitor: {visitor.VisitorName}", cancellationToken);

        var message = approvalRequired
            ? $"Visitor {visitor.VisitorName} has been registered and is pending approval."
            : $"Visitor {visitor.VisitorName} has been registered and approved.";

        TempData["Success"] = message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return RedirectToAction("Login", "Home");

        if (!await ResidentIsActiveAsync(userId.Value, cancellationToken))
            return await SignOutInactiveAsync();

        var visitor = await _db.VisitorRequests
            .Include(v => v.Resident)
            .SingleOrDefaultAsync(v => v.Uid == id && v.ResidentId == userId.Value, cancellationToken);

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
            DeclineReason = visitor.DeclineReason,
            Photos = photos
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return RedirectToAction("Login", "Home");

        if (!await ResidentIsActiveAsync(userId.Value, cancellationToken))
            return await SignOutInactiveAsync();

        var visitor = await _db.VisitorRequests
            .SingleOrDefaultAsync(v => v.Uid == id && v.ResidentId == userId.Value, cancellationToken);

        if (visitor is null)
            return NotFound();

        if (visitor.Status != VisitorStatus.Pending && visitor.Status != VisitorStatus.Approved)
        {
            TempData["Error"] = "Only pending or approved visitors can be cancelled.";
            return RedirectToAction(nameof(Index));
        }

        visitor.Status = VisitorStatus.Cancelled;
        visitor.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("Resident", userId, $"Cancelled visitor: {visitor.VisitorName} (ID: {visitor.Uid})", cancellationToken);

        TempData["Success"] = $"Visitor {visitor.VisitorName} has been cancelled.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> ResidentIsActiveAsync(int userId, CancellationToken cancellationToken)
    {
        return await _db.Residents.AnyAsync(r => r.Uid == userId && r.IsActive, cancellationToken);
    }

    private async Task<IActionResult> SignOutInactiveAsync()
    {
        await HttpContext.SignOutAsync(AuthSchemes.Resident);
        return RedirectToAction("Login", "Home");
    }
}
