using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResidentPortal.Data;
using ResidentPortal.Models;
using ResidentPortal.Security;
using ResidentPortal.Services;

namespace ResidentPortal.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Policy = AuthPolicies.StaffOnly, AuthenticationSchemes = AuthSchemes.Staff)]
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
    [AuthorizePermission(StaffPermissions.ComplaintsView)]
    public async Task<IActionResult> Index(ComplaintListViewModel filter, CancellationToken cancellationToken) =>
        View(await _queries.SearchAsync(filter, cancellationToken));

    [HttpGet]
    [AuthorizePermission(StaffPermissions.ComplaintsView)]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var entity = await _queries.GetDetailsEntityAsync(id, cancellationToken);
        return entity is null ? NotFound() : View(ResidentPortal.Controllers.ComplaintsController.MapDetails(entity, false));
    }

    [HttpGet]
    [AuthorizePermission(StaffPermissions.ComplaintsReportsBasic)]
    public async Task<IActionResult> Reports(CancellationToken cancellationToken) =>
        View(await _queries.BuildReportsAsync(
            advanced: User.HasPermission(StaffPermissions.ComplaintsReportsAdvanced),
            cancellationToken));

    [HttpGet]
    [AuthorizePermission(StaffPermissions.ComplaintsReportsAdvanced)]
    public async Task<IActionResult> ExportCsv(CancellationToken cancellationToken)
    {
        var bytes = await _queries.ExportCsvAsync(cancellationToken);
        return File(bytes, "text/csv", $"complaints-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    [HttpPost, ValidateAntiForgeryToken]
    [AuthorizePermission(StaffPermissions.ComplaintsManage)]
    public Task<IActionResult> Approve(int id, CancellationToken ct) =>
        StaffAction(id, actor => _workflow.ApproveAsync(id, actor, ct), ct);

    [HttpPost, ValidateAntiForgeryToken]
    [AuthorizePermission(StaffPermissions.ComplaintsManage)]
    public async Task<IActionResult> Decline(DeclineComplaintViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Decline reason is required.";
            return RedirectToAction(nameof(Details), new { id = model.ComplaintId });
        }
        return await StaffAction(model.ComplaintId,
            actor => _workflow.DeclineAsync(model.ComplaintId, model.Reason, actor, ct), ct);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [AuthorizePermission(StaffPermissions.ComplaintsManage)]
    public async Task<IActionResult> Assign(AssignComplaintViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Worker name is required.";
            return RedirectToAction(nameof(Details), new { id = model.ComplaintId });
        }
        return await StaffAction(model.ComplaintId,
            actor => _workflow.AssignAsync(model.ComplaintId, model.WorkerName, model.WorkerPhone, model.Note, actor, ct), ct);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [AuthorizePermission(StaffPermissions.ComplaintsManage)]
    public Task<IActionResult> StartWork(int id, CancellationToken ct) =>
        StaffAction(id, actor => _workflow.MoveStatusAsync(id, ComplaintStatus.WorkInProgress, "Work started.", actor, ct), ct);

    [HttpPost, ValidateAntiForgeryToken]
    [AuthorizePermission(StaffPermissions.ComplaintsManage)]
    public Task<IActionResult> Resolve(int id, CancellationToken ct) =>
        StaffAction(id, actor => _workflow.MoveStatusAsync(id, ComplaintStatus.Resolved, "Marked resolved.", actor, ct), ct);

    [HttpPost, ValidateAntiForgeryToken]
    [AuthorizePermission(StaffPermissions.ComplaintsManage)]
    public Task<IActionResult> Close(int id, CancellationToken ct) =>
        StaffAction(id, actor => _workflow.MoveStatusAsync(id, ComplaintStatus.Closed, "Closed by staff.", actor, ct), ct);

    [HttpPost, ValidateAntiForgeryToken]
    [AuthorizePermission(StaffPermissions.ComplaintsManage)]
    public async Task<IActionResult> Reopen(int id, CancellationToken ct)
    {
        var entity = await _queries.GetDetailsEntityAsync(id, ct);
        if (entity is null)
            return NotFound();
        var worker = string.IsNullOrWhiteSpace(entity.WorkerName) ? "Pending assignment" : entity.WorkerName;
        return await StaffAction(id,
            actor => _workflow.AssignAsync(id, worker, entity.WorkerPhone, "Reopened.", actor, ct), ct);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [AuthorizePermission(StaffPermissions.ComplaintsManage)]
    public async Task<IActionResult> AddNote(NoteComplaintViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Note is required.";
            return RedirectToAction(nameof(Details), new { id = model.ComplaintId });
        }
        return await StaffAction(model.ComplaintId,
            actor => _workflow.AddNoteAsync(model.ComplaintId, model.Note, actor, ct), ct);
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
        var userType = User.GetStaffUserType() ?? "Staff";
        string? name = null;

        if (userId is not null)
        {
            if (string.Equals(userType, PortalRoles.Admin, StringComparison.OrdinalIgnoreCase))
            {
                name = await _db.AdminUsers.AsNoTracking()
                    .Where(a => a.Uid == userId.Value).Select(a => a.FullName).SingleOrDefaultAsync(ct);
            }
            else if (string.Equals(userType, PortalRoles.Receptionist, StringComparison.OrdinalIgnoreCase))
            {
                name = await _db.Receptionists.AsNoTracking()
                    .Where(r => r.Uid == userId.Value).Select(r => r.FullName).SingleOrDefaultAsync(ct);
            }
        }

        return new ComplaintActor
        {
            UserType = userType,
            UserId = userId,
            DisplayName = name ?? User.Identity?.Name ?? userType
        };
    }
}
