using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResidentPortal.Data;
using ResidentPortal.Security;
using ResidentPortal.Services;

namespace ResidentPortal.Controllers;

/// <summary>Authorized photo download — not served as public static files.</summary>
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class VisitorPhotosController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly VisitorPhotoStorage _storage;

    public VisitorPhotosController(ApplicationDbContext db, VisitorPhotoStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    [HttpGet("/VisitorPhotos/{id:int}")]
    [Authorize(AuthenticationSchemes = AuthSchemes.Staff + "," + AuthSchemes.Resident)]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        var photo = await _db.VisitorPhotos
            .AsNoTracking()
            .Include(p => p.VisitorRequest)
            .SingleOrDefaultAsync(p => p.Uid == id, cancellationToken);

        if (photo is null)
            return NotFound();

        if (!await CanViewAsync(photo.VisitorRequest.ResidentId, cancellationToken))
            return Forbid();

        var stream = _storage.OpenRead(photo.FilePath);
        if (stream is null)
            return NotFound();

        return File(stream, photo.ContentType);
    }

    private async Task<bool> CanViewAsync(int residentId, CancellationToken cancellationToken)
    {
        if (User.IsInRole(PortalRoles.Admin) || User.IsInRole(PortalRoles.Receptionist))
            return true;

        if (!User.IsInRole(PortalRoles.Resident))
            return false;

        var userId = User.GetUserId();
        if (userId is null)
            return false;

        return userId.Value == residentId
            && await _db.Residents.AnyAsync(r => r.Uid == userId.Value && r.IsActive, cancellationToken);
    }
}
