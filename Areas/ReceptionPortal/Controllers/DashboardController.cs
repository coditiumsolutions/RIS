using Microsoft.AspNetCore.Authentication;
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

        var summary = await BuildSummaryAsync(cancellationToken);

        var model = new ReceptionistDashboardViewModel
        {
            ReceptionistName = receptionist.FullName,
            Username = receptionist.Username,
            ExpectedVisitorsToday = summary.ExpectedToday,
            CheckedInToday = summary.CheckedInNow,
            CheckedOutToday = summary.CheckedOutToday,
            WalkInsToday = summary.WalkInsToday,
            TodayIso = summary.Today
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Summary(CancellationToken cancellationToken)
    {
        if (!await IsActiveReceptionistAsync(cancellationToken))
            return Unauthorized();

        return Json(await BuildSummaryAsync(cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        string? q,
        string? date,
        string? status,
        int? offset,
        int? limit,
        CancellationToken cancellationToken)
    {
        if (!await IsActiveReceptionistAsync(cancellationToken))
            return Unauthorized();

        var today = _timeZone.Today;
        var term = (q ?? string.Empty).Trim();
        var statusFilter = NormalizeStatusFilter(status);
        DateOnly? arrivalDate = null;
        if (!string.IsNullOrWhiteSpace(date) && DateOnly.TryParse(date, out var parsedDate))
            arrivalDate = parsedDate;

        var take = Math.Clamp(limit ?? 25, 1, 100);
        var skip = Math.Max(offset ?? 0, 0);

        // Dashboard typeahead: empty query with no other filters stays empty.
        var browseList = arrivalDate.HasValue
            || !string.Equals(statusFilter, "all", StringComparison.Ordinal)
            || skip > 0
            || limit.HasValue;

        if (term.Length < 1 && !browseList)
        {
            return Json(new ReceptionDeskSearchDto
            {
                Query = term,
                Date = arrivalDate?.ToString("yyyy-MM-dd"),
                Status = statusFilter,
                Offset = skip,
                Limit = take,
                Total = 0,
                HasMore = false,
                Visitors = []
            });
        }

        var query = BuildDeskSearchQuery(term, arrivalDate, statusFilter, today);

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(v => v.ArrivalDate == today ? 1 : 0)
            .ThenBy(v => v.Status == VisitorStatus.CheckedIn ? 0 : v.Status == VisitorStatus.Approved ? 1 : 2)
            .ThenByDescending(v => v.UpdatedAt)
            .Skip(skip)
            .Take(take + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > take;
        if (hasMore)
            rows = rows.Take(take).ToList();

        return Json(new ReceptionDeskSearchDto
        {
            Query = term,
            Date = arrivalDate?.ToString("yyyy-MM-dd"),
            Status = statusFilter,
            Offset = skip,
            Limit = take,
            Total = total,
            HasMore = hasMore,
            Visitors = rows.Select(v => MapVisitor(v, today)).ToList()
        });
    }

    private IQueryable<VisitorRequest> BuildDeskSearchQuery(
        string term,
        DateOnly? arrivalDate,
        string statusFilter,
        DateOnly today)
    {
        var query = _db.VisitorRequests
            .AsNoTracking()
            .Include(v => v.Resident)
            .Where(v => v.Status == VisitorStatus.Approved ||
                        v.Status == VisitorStatus.CheckedIn ||
                        v.Status == VisitorStatus.CheckedOut ||
                        v.Status == VisitorStatus.Pending);

        if (!string.IsNullOrWhiteSpace(term))
        {
            var lower = term.ToLowerInvariant();
            var passCodeMatch = int.TryParse(term, out var passCode);

            query = query.Where(v =>
                v.VisitorName.ToLower().Contains(lower) ||
                v.Phone.Contains(term) ||
                (v.CNIC != null && v.CNIC.Contains(term)) ||
                (v.VehicleRegistrationNo != null && v.VehicleRegistrationNo.ToLower().Contains(lower)) ||
                v.Resident.ResidentName.ToLower().Contains(lower) ||
                (v.Resident.UnitNo != null && v.Resident.UnitNo.ToLower().Contains(lower)) ||
                (passCodeMatch && v.Uid == passCode));
        }

        if (arrivalDate.HasValue)
            query = query.Where(v => v.ArrivalDate == arrivalDate.Value);

        query = statusFilter switch
        {
            "expected" => query.Where(v =>
                (v.Status == VisitorStatus.Approved || v.Status == VisitorStatus.Pending) &&
                (arrivalDate.HasValue || v.ArrivalDate == today)),
            "approved" => query.Where(v => v.Status == VisitorStatus.Approved),
            "pending" => query.Where(v => v.Status == VisitorStatus.Pending),
            "checkedin" or "inside" => query.Where(v => v.Status == VisitorStatus.CheckedIn),
            "checkedout" => query.Where(v => v.Status == VisitorStatus.CheckedOut),
            "walkins" => query.Where(v => v.IsWalkIn),
            _ => query
        };

        return query;
    }

    private static string NormalizeStatusFilter(string? status)
    {
        var value = (status ?? "all").Trim().ToLowerInvariant();
        return value switch
        {
            "expected" or "approved" or "pending" or "checkedin" or "inside" or "checkedout" or "walkins" => value,
            _ => "all"
        };
    }

    [HttpGet]
    public async Task<IActionResult> Day(string? date, string? filter, CancellationToken cancellationToken)
    {
        if (!await IsActiveReceptionistAsync(cancellationToken))
            return Unauthorized();

        var today = _timeZone.Today;
        if (!DateOnly.TryParse(date, out var day))
            day = today;

        var mode = (filter ?? "all").Trim().ToLowerInvariant();
        var dayStartUtc = _timeZone.ConvertToUtc(day.ToDateTime(TimeOnly.MinValue));
        var nextDayStartUtc = _timeZone.ConvertToUtc(day.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var query = _db.VisitorRequests
            .AsNoTracking()
            .Include(v => v.Resident)
            .AsQueryable();

        query = mode switch
        {
            "expected" => query.Where(v => v.ArrivalDate == day &&
                (v.Status == VisitorStatus.Approved || v.Status == VisitorStatus.Pending)),
            "checkedin" or "inside" => query.Where(v => v.Status == VisitorStatus.CheckedIn &&
                (v.ArrivalDate == day || (v.CheckedInAt != null && v.CheckedInAt.Value >= dayStartUtc && v.CheckedInAt.Value < nextDayStartUtc))),
            "checkedout" => query.Where(v => v.CheckedOutAt != null &&
                v.CheckedOutAt.Value >= dayStartUtc &&
                v.CheckedOutAt.Value < nextDayStartUtc),
            "walkins" => query.Where(v => v.IsWalkIn && v.CreatedAt >= dayStartUtc && v.CreatedAt < nextDayStartUtc),
            _ => query.Where(v =>
                v.ArrivalDate == day ||
                (v.CheckedInAt != null && v.CheckedInAt.Value >= dayStartUtc && v.CheckedInAt.Value < nextDayStartUtc) ||
                (v.CheckedOutAt != null && v.CheckedOutAt.Value >= dayStartUtc && v.CheckedOutAt.Value < nextDayStartUtc))
        };

        // Keep the desk focused on actionable / completed visit states.
        query = query.Where(v =>
            v.Status == VisitorStatus.Pending ||
            v.Status == VisitorStatus.Approved ||
            v.Status == VisitorStatus.CheckedIn ||
            v.Status == VisitorStatus.CheckedOut);

        var rows = await query
            .OrderBy(v => v.Status == VisitorStatus.CheckedIn ? 0 : v.Status == VisitorStatus.Approved ? 1 : 2)
            .ThenBy(v => v.VisitorName)
            .Take(150)
            .ToListAsync(cancellationToken);

        return Json(new ReceptionDeskDayDto
        {
            Date = day.ToString("yyyy-MM-dd"),
            Filter = mode,
            Visitors = rows.Select(v => MapVisitor(v, today)).ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> CalendarMonth(int? year, int? month, CancellationToken cancellationToken)
    {
        if (!await IsActiveReceptionistAsync(cancellationToken))
            return Unauthorized();

        var today = _timeZone.Today;
        var y = year ?? today.Year;
        var m = month ?? today.Month;
        if (m is < 1 or > 12)
            m = today.Month;
        if (y is < 2000 or > 2100)
            y = today.Year;

        var monthStart = new DateOnly(y, m, 1);
        var monthEnd = monthStart.AddMonths(1);

        var grouped = await _db.VisitorRequests
            .AsNoTracking()
            .Where(v => v.ArrivalDate >= monthStart && v.ArrivalDate < monthEnd &&
                (v.Status == VisitorStatus.Pending ||
                 v.Status == VisitorStatus.Approved ||
                 v.Status == VisitorStatus.CheckedIn ||
                 v.Status == VisitorStatus.CheckedOut))
            .GroupBy(v => v.ArrivalDate)
            .Select(g => new
            {
                Date = g.Key,
                Expected = g.Count(v => v.Status == VisitorStatus.Approved || v.Status == VisitorStatus.Pending),
                CheckedIn = g.Count(v => v.Status == VisitorStatus.CheckedIn),
                CheckedOut = g.Count(v => v.Status == VisitorStatus.CheckedOut),
                Total = g.Count()
            })
            .ToListAsync(cancellationToken);

        var days = grouped
            .OrderBy(g => g.Date)
            .Select(g => new ReceptionDeskCalendarDayDto
            {
                Date = g.Date.ToString("yyyy-MM-dd"),
                Expected = g.Expected,
                CheckedIn = g.CheckedIn,
                CheckedOut = g.CheckedOut,
                Total = g.Total
            })
            .ToList();

        return Json(new ReceptionDeskCalendarDto
        {
            Year = y,
            Month = m,
            Days = days
        });
    }

    private async Task<ReceptionDeskSummaryDto> BuildSummaryAsync(CancellationToken cancellationToken)
    {
        var today = _timeZone.Today;
        var todayStartUtc = _timeZone.ConvertToUtc(today.ToDateTime(TimeOnly.MinValue));
        var tomorrowStartUtc = _timeZone.ConvertToUtc(today.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var expectedToday = await _db.VisitorRequests
            .CountAsync(v => v.ArrivalDate == today &&
                (v.Status == VisitorStatus.Approved || v.Status == VisitorStatus.Pending), cancellationToken);

        var checkedInNow = await _db.VisitorRequests
            .CountAsync(v => v.Status == VisitorStatus.CheckedIn, cancellationToken);

        var checkedOutToday = await _db.VisitorRequests
            .CountAsync(v => v.CheckedOutAt != null &&
                v.CheckedOutAt.Value >= todayStartUtc &&
                v.CheckedOutAt.Value < tomorrowStartUtc, cancellationToken);

        var walkInsToday = await _db.VisitorRequests
            .CountAsync(v => v.IsWalkIn && v.CreatedAt >= todayStartUtc && v.CreatedAt < tomorrowStartUtc, cancellationToken);

        return new ReceptionDeskSummaryDto
        {
            Today = today.ToString("yyyy-MM-dd"),
            ExpectedToday = expectedToday,
            CheckedInNow = checkedInNow,
            CheckedOutToday = checkedOutToday,
            WalkInsToday = walkInsToday
        };
    }

    private ReceptionDeskVisitorDto MapVisitor(VisitorRequest v, DateOnly today)
    {
        var canCheckIn = v.Status == VisitorStatus.Approved
            && VisitorDuration.IsWithinVisitWindow(v.ArrivalDate, v.Duration, today);

        return new ReceptionDeskVisitorDto
        {
            Id = v.Uid,
            VisitorName = v.VisitorName,
            Phone = v.Phone,
            Cnic = v.CNIC,
            Vehicle = v.VehicleRegistrationNo,
            Purpose = v.Purpose,
            Guests = v.NumberOfGuests,
            Status = v.Status,
            StatusLabel = StatusLabel(v.Status),
            ArrivalDate = v.ArrivalDate.ToString("yyyy-MM-dd"),
            ResidentName = v.Resident.ResidentName,
            ResidentUnit = v.Resident.UnitNo,
            IsWalkIn = v.IsWalkIn,
            CanCheckIn = canCheckIn,
            CanCheckOut = v.Status == VisitorStatus.CheckedIn,
            CheckedInAt = _timeZone.FormatDateTime(v.CheckedInAt, "HH:mm"),
            CheckedOutAt = _timeZone.FormatDateTime(v.CheckedOutAt, "HH:mm")
        };
    }

    private static string StatusLabel(string status) => status switch
    {
        VisitorStatus.CheckedIn => "Checked In",
        VisitorStatus.CheckedOut => "Checked Out",
        _ => status
    };

    private async Task<bool> IsActiveReceptionistAsync(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return false;

        return await _db.Receptionists
            .AsNoTracking()
            .AnyAsync(r => r.Uid == userId && r.IsActive, cancellationToken);
    }

    private async Task<IActionResult> SignOutInactiveAsync()
    {
        await HttpContext.SignOutAsync(AuthSchemes.Receptionist);
        return RedirectToAction("Index", "Account", new { area = "ReceptionPortal" });
    }
}
