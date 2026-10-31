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
public class ResidentsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly PasswordService _passwords;
    private readonly AuditLogger _audit;
    private readonly ResidentLookupService _lookup;

    public ResidentsController(
        ApplicationDbContext db,
        PasswordService passwords,
        AuditLogger audit,
        ResidentLookupService lookup)
    {
        _db = db;
        _passwords = passwords;
        _audit = audit;
        _lookup = lookup;
    }

    /// <summary>
    /// Typeahead for Walk-in (and other Staff resident pickers). Authorized like WalkIn, not Residents.Manage,
    /// so receptionists can search without resident-admin rights.
    /// </summary>
    [HttpGet]
    [AuthorizePermission(StaffPermissions.VisitorsWalkIn)]
    public async Task<IActionResult> Lookup(string? q, CancellationToken cancellationToken)
    {
        var items = await _lookup.SearchAsync(q, cancellationToken);
        return Json(items.Select(r => new
        {
            id = r.Id,
            name = r.Name,
            customerId = r.CustomerId,
            tower = r.Tower,
            towerDisplay = r.TowerDisplay,
            unitNo = r.UnitNo,
            block = r.Block,
            floor = r.Floor,
            street = r.Street,
            mobile = r.MobileMasked,
            mobileMasked = r.MobileMasked
        }));
    }

    [HttpGet]
    [AuthorizePermission(StaffPermissions.ResidentsManage)]
    public async Task<IActionResult> Index(string? q, CancellationToken cancellationToken)
    {
        var query = _db.Residents.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var searchTerm = q.Trim().ToLower();
            query = query.Where(r =>
                r.ResidentName.ToLower().Contains(searchTerm) ||
                r.Username.ToLower().Contains(searchTerm) ||
                r.CustomerId.ToLower().Contains(searchTerm) ||
                (r.UnitNo != null && r.UnitNo.ToLower().Contains(searchTerm)) ||
                (r.Mobile != null && r.Mobile.Contains(searchTerm)));
        }

        var residents = await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(100)
            .Select(r => new ResidentListItem
            {
                Uid = r.Uid,
                ResidentName = r.ResidentName,
                Username = r.Username,
                CustomerId = r.CustomerId,
                Email = r.Email,
                Mobile = r.Mobile,
                UnitNo = r.UnitNo,
                Tower = r.Tower,
                Block = r.Block,
                Status = r.Status,
                IsActive = r.IsActive,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var totalCount = await _db.Residents.CountAsync(cancellationToken);
        var activeCount = await _db.Residents.CountAsync(r => r.IsActive, cancellationToken);

        var model = new ResidentListViewModel
        {
            Residents = residents,
            TotalCount = totalCount,
            ActiveCount = activeCount,
            SearchQuery = q
        };

        return View(model);
    }

    [HttpGet]
    [AuthorizePermission(StaffPermissions.ResidentsManage)]
    public IActionResult Create()
    {
        return View(new CreateResidentViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AuthorizePermission(StaffPermissions.ResidentsManage)]
    public async Task<IActionResult> Create(CreateResidentViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var normalizedUsername = model.Username.Trim().ToLower();
        var existingUser = await _db.Residents.AnyAsync(r => r.Username.ToLower() == normalizedUsername, cancellationToken);
        if (existingUser)
        {
            ModelState.AddModelError(nameof(model.Username), "This username is already taken.");
            return View(model);
        }

        var customerId = model.CustomerId.Trim();
        var customerTaken = await _db.Residents.AnyAsync(
            r => r.CustomerId.ToLower() == customerId.ToLower(), cancellationToken);
        if (customerTaken)
        {
            ModelState.AddModelError(nameof(model.CustomerId), "This Customer ID is already assigned to another resident.");
            return View(model);
        }

        var userId = User.GetUserId();
        var resident = new Resident
        {
            ResidentName = model.ResidentName.Trim(),
            Username = model.Username.Trim(),
            CustomerId = customerId,
            PasswordHash = _passwords.Hash(model.Password),
            Email = model.Email?.Trim(),
            Mobile = model.Mobile?.Trim(),
            Mobile2 = model.Mobile2?.Trim(),
            UnitNo = model.UnitNo?.Trim(),
            Street = model.Street?.Trim(),
            Tower = model.Tower?.Trim(),
            Floor = model.Floor?.Trim(),
            Block = model.Block?.Trim(),
            Status = model.Status,
            IsActive = true,
            MustChangePassword = true,
            SecurityStamp = Guid.NewGuid().ToString("D").ToLowerInvariant(),
            CreatedAt = DateTime.UtcNow
        };

        _db.Residents.Add(resident);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(User.GetStaffUserType() ?? "Admin", userId, $"Created resident: {resident.ResidentName} ({resident.Username})", cancellationToken);

        TempData["Success"] = $"Resident {resident.ResidentName} has been created. They must change their password on first login.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [AuthorizePermission(StaffPermissions.ResidentsManage)]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var resident = await _db.Residents
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Uid == id, cancellationToken);

        if (resident is null)
            return NotFound();

        var model = new EditResidentViewModel
        {
            Uid = resident.Uid,
            ResidentName = resident.ResidentName,
            Username = resident.Username,
            CustomerId = resident.CustomerId,
            Email = resident.Email,
            Mobile = resident.Mobile,
            Mobile2 = resident.Mobile2,
            UnitNo = resident.UnitNo,
            Street = resident.Street,
            Tower = resident.Tower,
            Floor = resident.Floor,
            Block = resident.Block,
            Status = resident.Status,
            IsActive = resident.IsActive
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AuthorizePermission(StaffPermissions.ResidentsManage)]
    public async Task<IActionResult> Edit(EditResidentViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var resident = await _db.Residents.FindAsync(new object[] { model.Uid }, cancellationToken);
        if (resident is null)
            return NotFound();

        var normalizedUsername = model.Username.Trim().ToLower();
        var existingUser = await _db.Residents.AnyAsync(r => r.Uid != model.Uid && r.Username.ToLower() == normalizedUsername, cancellationToken);
        if (existingUser)
        {
            ModelState.AddModelError(nameof(model.Username), "This username is already taken.");
            return View(model);
        }

        var customerId = model.CustomerId.Trim();
        var customerTaken = await _db.Residents.AnyAsync(
            r => r.Uid != model.Uid && r.CustomerId.ToLower() == customerId.ToLower(), cancellationToken);
        if (customerTaken)
        {
            ModelState.AddModelError(nameof(model.CustomerId), "This Customer ID is already assigned to another resident.");
            return View(model);
        }

        var userId = User.GetUserId();
        var wasActive = resident.IsActive;
        resident.ResidentName = model.ResidentName.Trim();
        resident.Username = model.Username.Trim();
        resident.CustomerId = customerId;
        resident.Email = model.Email?.Trim();
        resident.Mobile = model.Mobile?.Trim();
        resident.Mobile2 = model.Mobile2?.Trim();
        resident.UnitNo = model.UnitNo?.Trim();
        resident.Street = model.Street?.Trim();
        resident.Tower = model.Tower?.Trim();
        resident.Floor = model.Floor?.Trim();
        resident.Block = model.Block?.Trim();
        resident.Status = model.Status;
        resident.IsActive = model.IsActive;

        if (wasActive && !model.IsActive)
            resident.SecurityStamp = Guid.NewGuid().ToString("D").ToLowerInvariant();

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(User.GetStaffUserType() ?? "Admin", userId, $"Updated resident: {resident.ResidentName} (ID: {resident.Uid})", cancellationToken);

        TempData["Success"] = $"Resident {resident.ResidentName} has been updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [AuthorizePermission(StaffPermissions.ResidentsManage)]
    public async Task<IActionResult> ResetPassword(int id, CancellationToken cancellationToken)
    {
        var resident = await _db.Residents
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Uid == id, cancellationToken);

        if (resident is null)
            return NotFound();

        var model = new ResetResidentPasswordViewModel
        {
            Uid = resident.Uid,
            ResidentName = resident.ResidentName
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AuthorizePermission(StaffPermissions.ResidentsManage)]
    public async Task<IActionResult> ResetPassword(ResetResidentPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var resident = await _db.Residents.FindAsync(new object[] { model.Uid }, cancellationToken);
        if (resident is null)
            return NotFound();

        var userId = User.GetUserId();
        resident.PasswordHash = _passwords.Hash(model.NewPassword);
        resident.MustChangePassword = model.MustChangePassword;
        resident.SecurityStamp = Guid.NewGuid().ToString("D").ToLowerInvariant();

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(User.GetStaffUserType() ?? "Admin", userId, $"Reset password for resident: {resident.ResidentName} (ID: {resident.Uid})", cancellationToken);

        TempData["Success"] = $"Password for {resident.ResidentName} has been reset.";
        return RedirectToAction(nameof(Index));
    }
}
