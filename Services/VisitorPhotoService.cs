using ResidentPortal.Data;
using ResidentPortal.Models;

namespace ResidentPortal.Services;

public class VisitorPhotoService
{
    private readonly ApplicationDbContext _db;
    private readonly VisitorPhotoStorage _storage;
    private readonly ILogger<VisitorPhotoService> _logger;

    public VisitorPhotoService(
        ApplicationDbContext db,
        VisitorPhotoStorage storage,
        ILogger<VisitorPhotoService> logger)
    {
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    public async Task<(bool Saved, string? Warning)> TryAttachAsync(
        int visitorRequestId,
        string kind,
        IFormFile? photo,
        int? receptionistId,
        int? adminId,
        CancellationToken cancellationToken)
    {
        if (photo is null || photo.Length <= 0)
            return (false, null);

        if (kind is not (VisitorPhotoKind.CheckIn or VisitorPhotoKind.CheckOut))
            return (false, "Invalid photo kind.");

        await using var stream = photo.OpenReadStream();
        var (ok, relativeName, error) = await _storage.SaveAsync(stream, photo.ContentType, cancellationToken);
        if (!ok || relativeName is null)
        {
            _logger.LogWarning("Visitor photo rejected for request {VisitorId}: {Error}", visitorRequestId, error);
            return (false, error ?? "Photo could not be saved.");
        }

        _db.VisitorPhotos.Add(new VisitorPhoto
        {
            VisitorRequestId = visitorRequestId,
            Kind = kind,
            FilePath = relativeName,
            ContentType = _storage.NormalizeContentType(photo.ContentType),
            SizeBytes = (int)Math.Min(photo.Length, int.MaxValue),
            CapturedByReceptionistId = receptionistId,
            CapturedByAdminId = adminId,
            CapturedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }
}
