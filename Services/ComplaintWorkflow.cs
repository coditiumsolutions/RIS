using Microsoft.EntityFrameworkCore;
using ResidentPortal.Data;
using ResidentPortal.Models;

namespace ResidentPortal.Services;

public sealed class ComplaintActor
{
    public required string UserType { get; init; }
    public int? UserId { get; init; }
    public string? DisplayName { get; init; }
}

public class ComplaintWorkflow
{
    private static readonly Dictionary<string, HashSet<string>> StaffTransitions = new(StringComparer.Ordinal)
    {
        [ComplaintStatus.Initiated] = [ComplaintStatus.Approved, ComplaintStatus.Declined, ComplaintStatus.Closed],
        [ComplaintStatus.Approved] = [ComplaintStatus.Assigned, ComplaintStatus.Closed],
        [ComplaintStatus.Assigned] = [ComplaintStatus.Assigned, ComplaintStatus.WorkInProgress, ComplaintStatus.Closed],
        [ComplaintStatus.WorkInProgress] = [ComplaintStatus.Resolved, ComplaintStatus.Closed],
        [ComplaintStatus.Resolved] = [ComplaintStatus.Closed, ComplaintStatus.Assigned],
        [ComplaintStatus.Closed] = [ComplaintStatus.Assigned],
        [ComplaintStatus.Declined] = [ComplaintStatus.Closed],
        [ComplaintStatus.Cancelled] = []
    };

    private readonly ApplicationDbContext _db;
    private readonly AppSettingsService _settings;
    private readonly ComplaintEmailNotifier _email;
    private readonly ComplaintPhotoStorage _photos;
    private readonly ILogger<ComplaintWorkflow> _logger;

    public ComplaintWorkflow(
        ApplicationDbContext db,
        AppSettingsService settings,
        ComplaintEmailNotifier email,
        ComplaintPhotoStorage photos,
        ILogger<ComplaintWorkflow> logger)
    {
        _db = db;
        _settings = settings;
        _email = email;
        _photos = photos;
        _logger = logger;
    }

    public async Task<int> GetSlaHoursAsync(string priority, CancellationToken ct)
    {
        var key = priority switch
        {
            ComplaintPriority.Urgent => AppSettingKeys.ComplaintSlaUrgentHours,
            ComplaintPriority.Low => AppSettingKeys.ComplaintSlaLowHours,
            _ => AppSettingKeys.ComplaintSlaNormalHours
        };
        var raw = await _settings.GetSettingAsync(key, ct);
        var fallback = priority switch
        {
            ComplaintPriority.Urgent => 24,
            ComplaintPriority.Low => 168,
            _ => 72
        };
        return int.TryParse(raw, out var hours) && hours > 0 ? hours : fallback;
    }

    public static bool IsOpenStatus(string status) =>
        status is ComplaintStatus.Initiated or ComplaintStatus.Approved or ComplaintStatus.Assigned
            or ComplaintStatus.WorkInProgress or ComplaintStatus.Resolved;

    public static bool IsOverdue(Complaint c, DateTime utcNow) =>
        c.SlaDueAtUtc is not null
        && c.SlaDueAtUtc.Value < utcNow
        && IsOpenStatus(c.Status)
        && c.Status is not ComplaintStatus.Resolved;

    public bool CanStaffTransition(string from, string to) =>
        StaffTransitions.TryGetValue(from, out var set) && set.Contains(to);

    public async Task<(bool Ok, string? Error, Complaint? Complaint)> CreateResidentComplaintAsync(
        int residentId,
        CreateComplaintViewModel model,
        IFormFile? photo,
        ComplaintActor actor,
        CancellationToken ct)
    {
        if (!ComplaintPriority.All.Contains(model.Priority))
            return (false, "Invalid priority.", null);

        var category = await _db.ComplaintCategories
            .SingleOrDefaultAsync(c => c.Uid == model.CategoryId && c.IsActive, ct);
        if (category is null)
            return (false, "Please select a valid category.", null);

        var now = DateTime.UtcNow;
        var slaHours = await GetSlaHoursAsync(model.Priority, ct);
        var complaint = new Complaint
        {
            ResidentId = residentId,
            CategoryId = category.Uid,
            Priority = model.Priority,
            Description = model.Description.Trim(),
            PreferredVisitTime = string.IsNullOrWhiteSpace(model.PreferredVisitTime) ? null : model.PreferredVisitTime.Trim(),
            Status = ComplaintStatus.Initiated,
            SlaDueAtUtc = now.AddHours(slaHours),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.Complaints.Add(complaint);
        AddHistory(complaint, actor, null, ComplaintStatus.Initiated, "Complaint submitted.");
        await _db.SaveChangesAsync(ct);

        if (photo is { Length: > 0 })
        {
            var (saved, warning) = await TryAttachPhotoAsync(complaint.Uid, photo, actor, ct);
            if (!saved && !string.IsNullOrEmpty(warning))
                _logger.LogWarning("Complaint {Id} created but photo skipped: {Warning}", complaint.Uid, warning);
        }

        return (true, null, complaint);
    }

    public async Task<(bool Ok, string? Error)> CancelAsync(int complaintId, int residentId, ComplaintActor actor, CancellationToken ct)
    {
        var complaint = await _db.Complaints.SingleOrDefaultAsync(c => c.Uid == complaintId && c.ResidentId == residentId, ct);
        if (complaint is null)
            return (false, "Complaint not found.");
        if (complaint.Status != ComplaintStatus.Initiated)
            return (false, "Only initiated complaints can be cancelled.");

        return await TransitionAsync(complaint, ComplaintStatus.Cancelled, actor, "Cancelled by resident.", notify: false, ct);
    }

    public async Task<(bool Ok, string? Error)> ApproveAsync(int complaintId, ComplaintActor actor, CancellationToken ct)
    {
        var complaint = await LoadAsync(complaintId, ct);
        if (complaint is null)
            return (false, "Complaint not found.");
        return await TransitionAsync(complaint, ComplaintStatus.Approved, actor, "Approved.", notify: true, ct);
    }

    public async Task<(bool Ok, string? Error)> DeclineAsync(int complaintId, string reason, ComplaintActor actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return (false, "Decline reason is required.");

        var complaint = await LoadAsync(complaintId, ct);
        if (complaint is null)
            return (false, "Complaint not found.");

        complaint.DeclineReason = reason.Trim();
        return await TransitionAsync(complaint, ComplaintStatus.Declined, actor, reason.Trim(), notify: true, ct);
    }

    public async Task<(bool Ok, string? Error)> AssignAsync(
        int complaintId, string workerName, string? workerPhone, string? note, ComplaintActor actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(workerName))
            return (false, "Worker name is required.");

        var complaint = await LoadAsync(complaintId, ct);
        if (complaint is null)
            return (false, "Complaint not found.");

        if (complaint.Status is not (ComplaintStatus.Approved or ComplaintStatus.Assigned or ComplaintStatus.Resolved or ComplaintStatus.Closed))
        {
            if (!CanStaffTransition(complaint.Status, ComplaintStatus.Assigned))
                return (false, $"Cannot assign from status {ComplaintStatus.Display(complaint.Status)}.");
        }

        var from = complaint.Status;
        var isReassign = from == ComplaintStatus.Assigned;
        var isReopen = from is ComplaintStatus.Resolved or ComplaintStatus.Closed;

        complaint.WorkerName = workerName.Trim();
        complaint.WorkerPhone = string.IsNullOrWhiteSpace(workerPhone) ? null : workerPhone.Trim();
        complaint.AssignedAtUtc ??= DateTime.UtcNow;
        if (isReopen)
        {
            complaint.ResolvedAtUtc = null;
            complaint.ClosedAtUtc = null;
        }

        complaint.Status = ComplaintStatus.Assigned;
        complaint.UpdatedAtUtc = DateTime.UtcNow;

        var historyNote = note?.Trim();
        if (string.IsNullOrWhiteSpace(historyNote))
        {
            historyNote = isReopen
                ? $"Reopened and assigned to {complaint.WorkerName}."
                : isReassign
                    ? $"Reassigned to {complaint.WorkerName}."
                    : $"Assigned to {complaint.WorkerName}.";
        }

        AddHistory(complaint, actor, from, ComplaintStatus.Assigned, historyNote);
        await _db.SaveChangesAsync(ct);
        await _email.NotifyStatusChangeAsync(complaint.Uid, ct);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> MoveStatusAsync(int complaintId, string toStatus, string? note, ComplaintActor actor, CancellationToken ct)
    {
        var complaint = await LoadAsync(complaintId, ct);
        if (complaint is null)
            return (false, "Complaint not found.");
        return await TransitionAsync(complaint, toStatus, actor, note, notify: true, ct);
    }

    public async Task<(bool Ok, string? Error)> AddNoteAsync(int complaintId, string note, ComplaintActor actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(note))
            return (false, "Note is required.");

        var complaint = await LoadAsync(complaintId, ct);
        if (complaint is null)
            return (false, "Complaint not found.");

        AddHistory(complaint, actor, complaint.Status, complaint.Status, note.Trim());
        complaint.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<(bool Saved, string? Warning)> TryAttachPhotoAsync(
        int complaintId, IFormFile photo, ComplaintActor actor, CancellationToken ct)
    {
        await using var stream = photo.OpenReadStream();
        var (ok, relativeName, error) = await _photos.SaveAsync(stream, photo.ContentType, ct);
        if (!ok || relativeName is null)
            return (false, error);

        _db.ComplaintPhotos.Add(new ComplaintPhoto
        {
            ComplaintId = complaintId,
            FilePath = relativeName,
            ContentType = _photos.NormalizeContentType(photo.ContentType),
            SizeBytes = (int)Math.Min(photo.Length, int.MaxValue),
            UploadedByUserType = actor.UserType,
            UploadedByUserId = actor.UserId,
            CapturedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    private async Task<Complaint?> LoadAsync(int id, CancellationToken ct) =>
        await _db.Complaints.Include(c => c.Resident).Include(c => c.Category)
            .SingleOrDefaultAsync(c => c.Uid == id, ct);

    private async Task<(bool Ok, string? Error)> TransitionAsync(
        Complaint complaint, string toStatus, ComplaintActor actor, string? note, bool notify, CancellationToken ct)
    {
        if (!CanStaffTransition(complaint.Status, toStatus) && actor.UserType != "Resident")
            return (false, $"Cannot move from {ComplaintStatus.Display(complaint.Status)} to {ComplaintStatus.Display(toStatus)}.");

        if (actor.UserType == "Resident" && !(complaint.Status == ComplaintStatus.Initiated && toStatus == ComplaintStatus.Cancelled))
            return (false, "Invalid transition.");

        var from = complaint.Status;
        complaint.Status = toStatus;
        complaint.UpdatedAtUtc = DateTime.UtcNow;

        if (toStatus == ComplaintStatus.Resolved)
            complaint.ResolvedAtUtc = DateTime.UtcNow;
        if (toStatus == ComplaintStatus.Closed)
            complaint.ClosedAtUtc = DateTime.UtcNow;
        if (toStatus == ComplaintStatus.Assigned)
            complaint.AssignedAtUtc ??= DateTime.UtcNow;

        AddHistory(complaint, actor, from, toStatus, note);
        await _db.SaveChangesAsync(ct);

        if (notify)
            await _email.NotifyStatusChangeAsync(complaint.Uid, ct);

        return (true, null);
    }

    private static void AddHistory(Complaint complaint, ComplaintActor actor, string? from, string? to, string? note)
    {
        complaint.History.Add(new ComplaintHistory
        {
            ActorUserType = actor.UserType,
            ActorUserId = actor.UserId,
            ActorDisplayName = actor.DisplayName,
            FromStatus = from,
            ToStatus = to,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        });
    }
}
