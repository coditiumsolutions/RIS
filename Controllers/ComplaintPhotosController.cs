using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResidentPortal.Data;
using ResidentPortal.Security;
using ResidentPortal.Services;

namespace ResidentPortal.Controllers;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ComplaintPhotosController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ComplaintPhotoStorage _storage;

    public ComplaintPhotosController(ApplicationDbContext db, ComplaintPhotoStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    [HttpGet("/ComplaintPhotos/{id:int}")]
    [Authorize(AuthenticationSchemes = AuthSchemes.Admin + "," + AuthSchemes.Receptionist + "," + AuthSchemes.Resident)]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        var photo = await _db.ComplaintPhotos
            .AsNoTracking()
            .Include(p => p.Complaint)
            .SingleOrDefaultAsync(p => p.Uid == id, cancellationToken);

        if (photo is null)
            return NotFound();

        if (User.IsInRole(PortalRoles.Admin) || User.IsInRole(PortalRoles.Receptionist))
        {
            // staff ok
        }
        else if (User.IsInRole(PortalRoles.Resident))
        {
            var userId = User.GetUserId();
            if (userId is null || photo.Complaint.ResidentId != userId.Value)
                return Forbid();
        }
        else
        {
            return Forbid();
        }

        var stream = _storage.OpenRead(photo.FilePath);
        return stream is null ? NotFound() : File(stream, photo.ContentType);
    }
}
