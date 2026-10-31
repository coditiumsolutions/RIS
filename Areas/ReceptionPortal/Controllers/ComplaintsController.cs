using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResidentPortal.Controllers;
using ResidentPortal.Data;
using ResidentPortal.Models;
using ResidentPortal.Security;
using ResidentPortal.Services;

namespace ResidentPortal.Areas.ReceptionPortal.Controllers;

[Area("ReceptionPortal")]
[Authorize(Policy = AuthPolicies.ReceptionistOnly)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ComplaintsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ComplaintWorkflow _workflow;
    private readonly ComplaintQueryService _queries;

    public ComplaintsController(ApplicationDbContext db, ComplaintWorkflow workflow, ComplaintQueryService queries)
    {
        _db = db;
        _workflow = workflow;
        _queries = queries;
    }

    [HttpGet]
    public async Task<IActionResult> Index(ComplaintListViewModel filter, CancellationToken cancellationToken)
    {
        return View(await _queries.SearchAsync(filter, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var entity = await _queries.GetDetailsEntityAsync(id, cancellationToken);
        return entity is null ? NotFound() : View(ComplaintsControllerMap.Details(entity));
    }

    [HttpGet]
    public async Task<IActionResult> Reports(CancellationToken cancellationToken) =>
        View(await _queries.BuildReportsAsync(advanced: false, cancellationToken));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Approve(int id, CancellationToken ct) =>
        StaffAction(id, async actor => await _workflow.ApproveAsync(id, actor, ct), ct);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Decline(DeclineComplaintViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Decline reason is required.";
            return RedirectToAction(nameof(Details), new { id = model.ComplaintId });
        }
        return await StaffAction(model.ComplaintId,
            async actor => await _workflow.DeclineAsync(model.ComplaintId, model.Reason, actor, ct), ct);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(AssignComplaintViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Worker name is required.";
            return RedirectToAction(nameof(Details), new { id = model.ComplaintId });
        }
        return await StaffAction(model.ComplaintId,
            async actor => await _workflow.AssignAsync(model.ComplaintId, model.WorkerName, model.WorkerPhone, model.Note, actor, ct), ct);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> StartWork(int id, CancellationToken ct) =>
        StaffAction(id, async actor => await _workflow.MoveStatusAsync(id, ComplaintStatus.WorkInProgress, "Work started.", actor, ct), ct);

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Resolve(int id, CancellationToken ct) =>
        StaffAction(id, async actor => await _workflow.MoveStatusAsync(id, ComplaintStatus.Resolved, "Marked resolved.", actor, ct), ct);

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Close(int id, CancellationToken ct) =>
        StaffAction(id, async actor => await _workflow.MoveStatusAsync(id, ComplaintStatus.Closed, "Closed by staff.", actor, ct), ct);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reopen(int id, CancellationToken ct)
    {
        var entity = await _queries.GetDetailsEntityAsync(id, ct);
        if (entity is null)
            return NotFound();
        var worker = string.IsNullOrWhiteSpace(entity.WorkerName) ? "Pending assignment" : entity.WorkerName;
        return await StaffAction(id,
            async actor => await _workflow.AssignAsync(id, worker, entity.WorkerPhone, "Reopened.", actor, ct), ct);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddNote(NoteComplaintViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Note is required.";
            return RedirectToAction(nameof(Details), new { id = model.ComplaintId });
        }
        return await StaffAction(model.ComplaintId,
            async actor => await _workflow.AddNoteAsync(model.ComplaintId, model.Note, actor, ct), ct);
    }

    private async Task<IActionResult> StaffAction(int id, Func<ComplaintActor, Task<(bool Ok, string? Error)>> action, CancellationToken ct)
    {
        var actor = await BuildActorAsync(ct);
        var (ok, error) = await action(actor);
        TempData[ok ? "Success" : "Error"] = ok ? "Complaint updated." : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<ComplaintActor> BuildActorAsync(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var name = userId is null ? User.Identity?.Name : await _db.Receptionists.AsNoTracking()
            .Where(r => r.Uid == userId.Value).Select(r => r.FullName).SingleOrDefaultAsync(ct);
        return new ComplaintActor { UserType = "Receptionist", UserId = userId, DisplayName = name ?? "Receptionist" };
    }
}

internal static class ComplaintsControllerMap
{
    public static ComplaintDetailsViewModel Details(Complaint entity) =>
        ResidentPortal.Controllers.ComplaintsController.MapDetails(entity, canCancel: false);
}
