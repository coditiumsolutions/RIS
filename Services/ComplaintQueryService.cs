using Microsoft.EntityFrameworkCore;
using ResidentPortal.Data;
using ResidentPortal.Models;

namespace ResidentPortal.Services;

public class ComplaintQueryService
{
    private readonly ApplicationDbContext _db;
    private readonly BusinessTimeZone _timeZone;

    public ComplaintQueryService(ApplicationDbContext db, BusinessTimeZone timeZone)
    {
        _db = db;
        _timeZone = timeZone;
    }

    public async Task<ComplaintListViewModel> SearchAsync(ComplaintListViewModel filter, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var today = _timeZone.Today;
        var todayStart = _timeZone.ConvertToUtc(today.ToDateTime(TimeOnly.MinValue));
        var tomorrowStart = _timeZone.ConvertToUtc(today.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var query = _db.Complaints
            .AsNoTracking()
            .Include(c => c.Category)
            .Include(c => c.Resident)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.StatusFilter))
            query = query.Where(c => c.Status == filter.StatusFilter);
        if (!string.IsNullOrWhiteSpace(filter.PriorityFilter))
            query = query.Where(c => c.Priority == filter.PriorityFilter);
        if (filter.CategoryFilter is > 0)
            query = query.Where(c => c.CategoryId == filter.CategoryFilter);
        if (!string.IsNullOrWhiteSpace(filter.UnitFilter))
        {
            var unit = filter.UnitFilter.Trim().ToLower();
            query = query.Where(c => c.Resident.UnitNo != null && c.Resident.UnitNo.ToLower().Contains(unit));
        }
        if (!string.IsNullOrWhiteSpace(filter.ResidentFilter))
        {
            var name = filter.ResidentFilter.Trim().ToLower();
            query = query.Where(c => c.Resident.ResidentName.ToLower().Contains(name));
        }
        if (!string.IsNullOrWhiteSpace(filter.WorkerFilter))
        {
            var worker = filter.WorkerFilter.Trim().ToLower();
            query = query.Where(c => c.WorkerName != null && c.WorkerName.ToLower().Contains(worker));
        }
        if (filter.FromDate is not null)
        {
            var fromUtc = _timeZone.ConvertToUtc(filter.FromDate.Value.ToDateTime(TimeOnly.MinValue));
            query = query.Where(c => c.CreatedAtUtc >= fromUtc);
        }
        if (filter.ToDate is not null)
        {
            var toUtc = _timeZone.ConvertToUtc(filter.ToDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));
            query = query.Where(c => c.CreatedAtUtc < toUtc);
        }

        var items = await query
            .OrderByDescending(c => c.CreatedAtUtc)
            .Take(200)
            .Select(c => new ComplaintListItemViewModel
            {
                Uid = c.Uid,
                CategoryName = c.Category.Name,
                Priority = c.Priority,
                Status = c.Status,
                DescriptionPreview = c.Description.Length > 100 ? c.Description.Substring(0, 100) + "…" : c.Description,
                ResidentName = c.Resident.ResidentName,
                ResidentUnit = c.Resident.UnitNo,
                WorkerName = c.WorkerName,
                CreatedAtUtc = c.CreatedAtUtc,
                SlaDueAtUtc = c.SlaDueAtUtc,
                IsOverdue = c.SlaDueAtUtc != null && c.SlaDueAtUtc < now
                    && (c.Status == ComplaintStatus.Initiated || c.Status == ComplaintStatus.Approved
                        || c.Status == ComplaintStatus.Assigned || c.Status == ComplaintStatus.WorkInProgress)
            })
            .ToListAsync(ct);

        var openStatuses = new[]
        {
            ComplaintStatus.Initiated, ComplaintStatus.Approved, ComplaintStatus.Assigned,
            ComplaintStatus.WorkInProgress, ComplaintStatus.Resolved
        };

        filter.Items = items;
        filter.Categories = await _db.ComplaintCategories.AsNoTracking()
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync(ct);
        filter.OpenCount = await _db.Complaints.CountAsync(c => openStatuses.Contains(c.Status), ct);
        filter.OverdueCount = await _db.Complaints.CountAsync(c =>
            c.SlaDueAtUtc != null && c.SlaDueAtUtc < now
            && (c.Status == ComplaintStatus.Initiated || c.Status == ComplaintStatus.Approved
                || c.Status == ComplaintStatus.Assigned || c.Status == ComplaintStatus.WorkInProgress), ct);
        filter.TodayCount = await _db.Complaints.CountAsync(c => c.CreatedAtUtc >= todayStart && c.CreatedAtUtc < tomorrowStart, ct);
        return filter;
    }

    public async Task<Complaint?> GetDetailsEntityAsync(int id, CancellationToken ct) =>
        await _db.Complaints
            .Include(c => c.Category)
            .Include(c => c.Resident)
            .Include(c => c.History)
            .Include(c => c.Photos)
            .SingleOrDefaultAsync(c => c.Uid == id, ct);

    public async Task<ComplaintReportsViewModel> BuildReportsAsync(bool advanced, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var today = _timeZone.Today;
        var todayStart = _timeZone.ConvertToUtc(today.ToDateTime(TimeOnly.MinValue));
        var tomorrowStart = _timeZone.ConvertToUtc(today.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var model = new ComplaintReportsViewModel
        {
            IsAdvanced = advanced,
            OpenInitiated = await _db.Complaints.CountAsync(c => c.Status == ComplaintStatus.Initiated, ct),
            OpenApproved = await _db.Complaints.CountAsync(c => c.Status == ComplaintStatus.Approved, ct),
            OpenAssigned = await _db.Complaints.CountAsync(c => c.Status == ComplaintStatus.Assigned, ct),
            OpenInProgress = await _db.Complaints.CountAsync(c => c.Status == ComplaintStatus.WorkInProgress, ct),
            OpenResolved = await _db.Complaints.CountAsync(c => c.Status == ComplaintStatus.Resolved, ct),
            TodayCreated = await _db.Complaints.CountAsync(c => c.CreatedAtUtc >= todayStart && c.CreatedAtUtc < tomorrowStart, ct),
            OverdueCount = await _db.Complaints.CountAsync(c =>
                c.SlaDueAtUtc != null && c.SlaDueAtUtc < now
                && (c.Status == ComplaintStatus.Initiated || c.Status == ComplaintStatus.Approved
                    || c.Status == ComplaintStatus.Assigned || c.Status == ComplaintStatus.WorkInProgress), ct)
        };

        if (!advanced)
            return model;

        var resolved = await _db.Complaints.AsNoTracking()
            .Where(c => c.ResolvedAtUtc != null)
            .Select(c => new { c.CreatedAtUtc, Resolved = c.ResolvedAtUtc!.Value })
            .ToListAsync(ct);
        if (resolved.Count > 0)
            model.AvgResolutionHours = resolved.Average(x => (x.Resolved - x.CreatedAtUtc).TotalHours);

        model.WorkerWorkload = (await _db.Complaints.AsNoTracking()
            .Where(c => c.WorkerName != null && c.WorkerName != ""
                && (c.Status == ComplaintStatus.Assigned || c.Status == ComplaintStatus.WorkInProgress))
            .GroupBy(c => c.WorkerName!)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(20)
            .ToListAsync(ct))
            .Select(x => (x.Key, x.Count))
            .ToList();

        model.ByCategory = (await _db.Complaints.AsNoTracking()
            .GroupBy(c => c.Category.Name)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(ct))
            .Select(x => (x.Key, x.Count))
            .ToList();

        model.ByPriority = (await _db.Complaints.AsNoTracking()
            .GroupBy(c => c.Priority)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToListAsync(ct))
            .Select(x => (x.Key, x.Count))
            .ToList();

        model.RepeatByUnit = (await _db.Complaints.AsNoTracking()
            .Where(c => c.Resident.UnitNo != null && c.Resident.UnitNo != "")
            .GroupBy(c => c.Resident.UnitNo!)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .Where(x => x.Count >= 2)
            .OrderByDescending(x => x.Count)
            .Take(20)
            .ToListAsync(ct))
            .Select(x => (x.Key, x.Count))
            .ToList();

        var since = now.AddMonths(-11);
        var monthRows = await _db.Complaints.AsNoTracking()
            .Where(c => c.CreatedAtUtc >= since)
            .Select(c => c.CreatedAtUtc)
            .ToListAsync(ct);
        model.MonthlyTrend = monthRows
            .GroupBy(d => new DateTime(d.Year, d.Month, 1))
            .OrderBy(g => g.Key)
            .Select(g => (g.Key.ToString("yyyy-MM"), g.Count()))
            .ToList();

        return model;
    }

    public async Task<byte[]> ExportCsvAsync(CancellationToken ct)
    {
        var rows = await _db.Complaints.AsNoTracking()
            .Include(c => c.Category)
            .Include(c => c.Resident)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(ct);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Id,Resident,Unit,Category,Priority,Status,Worker,CreatedAtUtc,SlaDueAtUtc,ResolvedAtUtc");
        foreach (var c in rows)
        {
            string Esc(string? v) => "\"" + (v ?? string.Empty).Replace("\"", "\"\"") + "\"";
            sb.Append(c.Uid).Append(',')
                .Append(Esc(c.Resident.ResidentName)).Append(',')
                .Append(Esc(c.Resident.UnitNo)).Append(',')
                .Append(Esc(c.Category.Name)).Append(',')
                .Append(Esc(c.Priority)).Append(',')
                .Append(Esc(c.Status)).Append(',')
                .Append(Esc(c.WorkerName)).Append(',')
                .Append(c.CreatedAtUtc.ToString("o")).Append(',')
                .Append(c.SlaDueAtUtc?.ToString("o") ?? "").Append(',')
                .Append(c.ResolvedAtUtc?.ToString("o") ?? "")
                .AppendLine();
        }
        return System.Text.Encoding.UTF8.GetBytes(sb.ToString());
    }
}
