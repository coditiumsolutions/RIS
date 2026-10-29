using Microsoft.EntityFrameworkCore;
using ResidentPortal.Data;

namespace ResidentPortal.Services;

public sealed class SecurityStampService
{
    private readonly ApplicationDbContext _db;

    public SecurityStampService(ApplicationDbContext db)
    {
        _db = db;
    }

    public static string GenerateStamp() => Guid.NewGuid().ToString("D").ToLowerInvariant();

    public async Task<string?> GetStampAsync(string userType, int userId, CancellationToken cancellationToken)
    {
        return userType switch
        {
            "Resident" => await _db.Residents.Where(r => r.Uid == userId).Select(r => r.SecurityStamp).FirstOrDefaultAsync(cancellationToken),
            "Admin" => await _db.AdminUsers.Where(a => a.Uid == userId).Select(a => a.SecurityStamp).FirstOrDefaultAsync(cancellationToken),
            "Receptionist" => await _db.Receptionists.Where(r => r.Uid == userId).Select(r => r.SecurityStamp).FirstOrDefaultAsync(cancellationToken),
            _ => null
        };
    }

    public async Task<bool> ValidateStampAsync(string userType, int userId, string? stamp, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stamp))
            return false;

        var currentStamp = await GetStampAsync(userType, userId, cancellationToken);
        return string.Equals(currentStamp, stamp, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<string> RotateStampAsync(string userType, int userId, CancellationToken cancellationToken)
    {
        var newStamp = GenerateStamp();

        switch (userType)
        {
            case "Resident":
                var resident = await _db.Residents.FindAsync(new object[] { userId }, cancellationToken);
                if (resident is not null)
                    resident.SecurityStamp = newStamp;
                break;
            case "Admin":
                var admin = await _db.AdminUsers.FindAsync(new object[] { userId }, cancellationToken);
                if (admin is not null)
                    admin.SecurityStamp = newStamp;
                break;
            case "Receptionist":
                var receptionist = await _db.Receptionists.FindAsync(new object[] { userId }, cancellationToken);
                if (receptionist is not null)
                    receptionist.SecurityStamp = newStamp;
                break;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return newStamp;
    }

    public async Task<(bool IsActive, string? Stamp)> GetUserStatusAsync(string userType, int userId, CancellationToken cancellationToken)
    {
        switch (userType)
        {
            case "Resident":
            {
                var row = await _db.Residents.AsNoTracking()
                    .Where(r => r.Uid == userId)
                    .Select(r => new { r.IsActive, r.SecurityStamp })
                    .FirstOrDefaultAsync(cancellationToken);
                return row is null ? (false, null) : (row.IsActive, row.SecurityStamp);
            }
            case "Admin":
            {
                var row = await _db.AdminUsers.AsNoTracking()
                    .Where(a => a.Uid == userId)
                    .Select(a => new { a.IsActive, a.SecurityStamp })
                    .FirstOrDefaultAsync(cancellationToken);
                return row is null ? (false, null) : (row.IsActive, row.SecurityStamp);
            }
            case "Receptionist":
            {
                var row = await _db.Receptionists.AsNoTracking()
                    .Where(r => r.Uid == userId)
                    .Select(r => new { r.IsActive, r.SecurityStamp })
                    .FirstOrDefaultAsync(cancellationToken);
                return row is null ? (false, null) : (row.IsActive, row.SecurityStamp);
            }
            default:
                return (false, null);
        }
    }
}
