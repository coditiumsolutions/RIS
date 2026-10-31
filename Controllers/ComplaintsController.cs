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
public class ComplaintsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ComplaintWorkflow _workflow;

    public ComplaintsController(ApplicationDbContext db, ComplaintWorkflow workflow)
    {
        _db = db;
        _workflow = workflow;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = await RequireResidentIdAsync();
        if (userId is null)
            return RedirectToLogin();

        var now = DateTime.UtcNow;
        var items = await _db.Complaints
            .AsNoTracking()
            .Include(c => c.Category)
            .Where(c => c.ResidentId == userId.Value)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new ComplaintListItemViewModel
            {
                Uid = c.Uid,
                CategoryName = c.Category.Name,
                Priority = c.Priority,
                Status = c.Status,
                DescriptionPreview = c.Description.Length > 120 ? c.Description.Substring(0, 120) + "…" : c.Description,
                CreatedAtUtc = c.CreatedAtUtc,
                SlaDueAtUtc = c.SlaDueAtUtc,
                IsOverdue = c.SlaDueAtUtc != null && c.SlaDueAtUtc < now
                    && (c.Status == ComplaintStatus.Initiated || c.Status == ComplaintStatus.Approved
                        || c.Status == ComplaintStatus.Assigned || c.Status == ComplaintStatus.WorkInProgress)
            })
            .ToListAsync(cancellationToken);

        return View(new ComplaintListViewModel { Items = items });
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        if (await RequireResidentIdAsync() is null)
            return RedirectToLogin();

        return View(new CreateComplaintViewModel
        {
            Categories = await ActiveCategoriesAsync(cancellationToken)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> Create(CreateComplaintViewModel model, IFormFile? photo, CancellationToken cancellationToken)
    {
        var userId = await RequireResidentIdAsync();
        if (userId is null)
            return RedirectToLogin();

        model.Categories = await ActiveCategoriesAsync(cancellationToken);
        if (!ModelState.IsValid)
            return View(model);

        var actor = await BuildActorAsync(userId.Value, cancellationToken);
        var (ok, error, complaint) = await _workflow.CreateResidentComplaintAsync(userId.Value, model, photo, actor, cancellationToken);
        if (!ok || complaint is null)
        {
            ModelState.AddModelError(string.Empty, error ?? "Could not create complaint.");
            return View(model);
        }

        TempData["Success"] = $"Complaint #{complaint.Uid} has been submitted.";
        return RedirectToAction(nameof(Details), new { id = complaint.Uid });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var userId = await RequireResidentIdAsync();
        if (userId is null)
            return RedirectToLogin();

        var model = await BuildDetailsAsync(id, userId.Value, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var userId = await RequireResidentIdAsync();
        if (userId is null)
            return RedirectToLogin();

        var actor = await BuildActorAsync(userId.Value, cancellationToken);
        var (ok, error) = await _workflow.CancelAsync(id, userId.Value, actor, cancellationToken);
        TempData[ok ? "Success" : "Error"] = ok ? "Complaint cancelled." : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<ComplaintDetailsViewModel?> BuildDetailsAsync(int id, int residentId, CancellationToken ct)
    {
        var complaint = await _db.Complaints
            .AsNoTracking()
            .Include(c => c.Category)
            .Include(c => c.History)
            .Include(c => c.Photos)
            .Include(c => c.Resident)
            .SingleOrDefaultAsync(c => c.Uid == id && c.ResidentId == residentId, ct);

        if (complaint is null)
            return null;

        return MapDetails(complaint, canCancel: complaint.Status == ComplaintStatus.Initiated);
    }

    internal static ComplaintDetailsViewModel MapDetails(Complaint complaint, bool canCancel)
    {
        var now = DateTime.UtcNow;
        return new ComplaintDetailsViewModel
        {
            Uid = complaint.Uid,
            CategoryName = complaint.Category.Name,
            Priority = complaint.Priority,
            Status = complaint.Status,
            Description = complaint.Description,
            PreferredVisitTime = complaint.PreferredVisitTime,
            DeclineReason = complaint.DeclineReason,
            WorkerName = complaint.WorkerName,
            WorkerPhone = complaint.WorkerPhone,
            ResidentName = complaint.Resident.ResidentName,
            ResidentUnit = complaint.Resident.UnitNo,
            ResidentEmail = complaint.Resident.Email,
            ResidentMobile = complaint.Resident.Mobile,
            CreatedAtUtc = complaint.CreatedAtUtc,
            SlaDueAtUtc = complaint.SlaDueAtUtc,
            IsOverdue = ComplaintWorkflow.IsOverdue(complaint, now),
            CanCancel = canCancel,
            History = complaint.History
                .OrderBy(h => h.CreatedAtUtc)
                .Select(h => new ComplaintHistoryItemViewModel
                {
                    FromStatus = h.FromStatus,
                    ToStatus = h.ToStatus,
                    Note = h.Note,
                    ActorUserType = h.ActorUserType,
                    ActorDisplayName = h.ActorDisplayName,
                    CreatedAtUtc = h.CreatedAtUtc
                }).ToList(),
            Photos = complaint.Photos
                .OrderBy(p => p.CapturedAtUtc)
                .Select(p => new ComplaintPhotoItemViewModel { Uid = p.Uid, CapturedAtUtc = p.CapturedAtUtc })
                .ToList(),
            DeclineForm = new DeclineComplaintViewModel { ComplaintId = complaint.Uid },
            AssignForm = new AssignComplaintViewModel
            {
                ComplaintId = complaint.Uid,
                WorkerName = complaint.WorkerName ?? string.Empty,
                WorkerPhone = complaint.WorkerPhone
            },
            NoteForm = new NoteComplaintViewModel { ComplaintId = complaint.Uid }
        };
    }

    private async Task<IReadOnlyList<ComplaintCategory>> ActiveCategoriesAsync(CancellationToken ct) =>
        await _db.ComplaintCategories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .ToListAsync(ct);

    private async Task<int?> RequireResidentIdAsync()
    {
        var userId = User.GetUserId();
        if (userId is null)
            return null;
        var active = await _db.Residents.AnyAsync(r => r.Uid == userId.Value && r.IsActive);
        return active ? userId : null;
    }

    private async Task<ComplaintActor> BuildActorAsync(int userId, CancellationToken ct)
    {
        var name = await _db.Residents.AsNoTracking()
            .Where(r => r.Uid == userId)
            .Select(r => r.ResidentName)
            .SingleAsync(ct);
        return new ComplaintActor { UserType = "Resident", UserId = userId, DisplayName = name };
    }

    private IActionResult RedirectToLogin() => RedirectToAction("Login", "Home");
}
