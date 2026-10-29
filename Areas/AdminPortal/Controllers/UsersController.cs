using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResidentPortal.Data;
using ResidentPortal.Models;
using ResidentPortal.Security;
using ResidentPortal.Services;

namespace ResidentPortal.Areas.AdminPortal.Controllers;

[Area("AdminPortal")]
[Authorize(Policy = AuthPolicies.AdminOnly)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class UsersController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly PasswordService _passwords;
    private readonly AuditLogger _audit;

    public UsersController(ApplicationDbContext db, PasswordService passwords, AuditLogger audit)
    {
        _db = db;
        _passwords = passwords;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var admins = await _db.AdminUsers
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AdminUserListItem
            {
                Uid = a.Uid,
                FullName = a.FullName,
                Username = a.Username,
                Email = a.Email,
                IsActive = a.IsActive,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var receptionists = await _db.Receptionists
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReceptionistListItem
            {
                Uid = r.Uid,
                FullName = r.FullName,
                Username = r.Username,
                Email = r.Email,
                IsActive = r.IsActive,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var model = new UserListViewModel
        {
            Admins = admins,
            Receptionists = receptionists,
            TotalAdminCount = admins.Count,
            TotalReceptionistCount = receptionists.Count
        };

        return View(model);
    }

    [HttpGet]
    public IActionResult CreateReceptionist()
    {
        return View(new CreateReceptionistViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateReceptionist(CreateReceptionistViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var normalizedUsername = model.Username.Trim().ToLower();
        var existingUser = await _db.Receptionists.AnyAsync(r => r.Username.ToLower() == normalizedUsername, cancellationToken);
        if (existingUser)
        {
            ModelState.AddModelError(nameof(model.Username), "This username is already taken.");
            return View(model);
        }

        var userId = User.GetUserId();
        var receptionist = new Receptionist
        {
            FullName = model.FullName.Trim(),
            Username = model.Username.Trim(),
            PasswordHash = _passwords.Hash(model.Password),
            Email = model.Email?.Trim(),
            IsActive = true,
            MustChangePassword = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Receptionists.Add(receptionist);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("Admin", userId, $"Created receptionist: {receptionist.FullName} ({receptionist.Username})", cancellationToken);

        TempData["Success"] = $"Receptionist {receptionist.FullName} has been created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> EditReceptionist(int id, CancellationToken cancellationToken)
    {
        var receptionist = await _db.Receptionists
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Uid == id, cancellationToken);

        if (receptionist is null)
            return NotFound();

        var model = new EditReceptionistViewModel
        {
            Uid = receptionist.Uid,
            FullName = receptionist.FullName,
            Username = receptionist.Username,
            Email = receptionist.Email,
            IsActive = receptionist.IsActive
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditReceptionist(EditReceptionistViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var receptionist = await _db.Receptionists.FindAsync(new object[] { model.Uid }, cancellationToken);
        if (receptionist is null)
            return NotFound();

        var normalizedUsername = model.Username.Trim().ToLower();
        var existingUser = await _db.Receptionists.AnyAsync(r => r.Uid != model.Uid && r.Username.ToLower() == normalizedUsername, cancellationToken);
        if (existingUser)
        {
            ModelState.AddModelError(nameof(model.Username), "This username is already taken.");
            return View(model);
        }

        var userId = User.GetUserId();
        receptionist.FullName = model.FullName.Trim();
        receptionist.Username = model.Username.Trim();
        receptionist.Email = model.Email?.Trim();
        receptionist.IsActive = model.IsActive;

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("Admin", userId, $"Updated receptionist: {receptionist.FullName} (ID: {receptionist.Uid})", cancellationToken);

        TempData["Success"] = $"Receptionist {receptionist.FullName} has been updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ResetReceptionistPassword(int id, CancellationToken cancellationToken)
    {
        var receptionist = await _db.Receptionists
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Uid == id, cancellationToken);

        if (receptionist is null)
            return NotFound();

        var model = new ResetUserPasswordViewModel
        {
            Uid = receptionist.Uid,
            UserName = receptionist.FullName,
            UserType = "Receptionist"
        };

        return View("ResetPassword", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetReceptionistPassword(ResetUserPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View("ResetPassword", model);

        var receptionist = await _db.Receptionists.FindAsync(new object[] { model.Uid }, cancellationToken);
        if (receptionist is null)
            return NotFound();

        var userId = User.GetUserId();
        receptionist.PasswordHash = _passwords.Hash(model.NewPassword);
        receptionist.MustChangePassword = model.MustChangePassword;

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("Admin", userId, $"Reset password for receptionist: {receptionist.FullName} (ID: {receptionist.Uid})", cancellationToken);

        TempData["Success"] = $"Password for {receptionist.FullName} has been reset.";
        return RedirectToAction(nameof(Index));
    }
}
