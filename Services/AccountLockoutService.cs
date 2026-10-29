using Microsoft.EntityFrameworkCore;
using ResidentPortal.Data;

namespace ResidentPortal.Services;

public sealed class AccountLockoutService
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;

    public AccountLockoutService(ApplicationDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public int MaxFailedAttempts => _config.GetValue("Lockout:MaxFailedAttempts", 5);
    public int LockoutMinutes => _config.GetValue("Lockout:LockoutMinutes", 15);

    public async Task<bool> IsLockedOutAsync(string userType, int userId, CancellationToken cancellationToken)
    {
        var lockoutEnd = userType switch
        {
            "Resident" => await _db.Residents.Where(r => r.Uid == userId).Select(r => r.LockoutEndUtc).FirstOrDefaultAsync(cancellationToken),
            "Admin" => await _db.AdminUsers.Where(a => a.Uid == userId).Select(a => a.LockoutEndUtc).FirstOrDefaultAsync(cancellationToken),
            "Receptionist" => await _db.Receptionists.Where(r => r.Uid == userId).Select(r => r.LockoutEndUtc).FirstOrDefaultAsync(cancellationToken),
            _ => null
        };

        return lockoutEnd.HasValue && lockoutEnd.Value > DateTime.UtcNow;
    }

    public async Task RecordFailedLoginAsync(string userType, int userId, CancellationToken cancellationToken)
    {
        switch (userType)
        {
            case "Resident":
                var resident = await _db.Residents.FindAsync(new object[] { userId }, cancellationToken);
                if (resident is not null)
                {
                    resident.FailedLoginAttempts++;
                    if (resident.FailedLoginAttempts >= MaxFailedAttempts)
                    {
                        resident.LockoutEndUtc = DateTime.UtcNow.AddMinutes(LockoutMinutes);
                    }
                }
                break;
            case "Admin":
                var admin = await _db.AdminUsers.FindAsync(new object[] { userId }, cancellationToken);
                if (admin is not null)
                {
                    admin.FailedLoginAttempts++;
                    if (admin.FailedLoginAttempts >= MaxFailedAttempts)
                    {
                        admin.LockoutEndUtc = DateTime.UtcNow.AddMinutes(LockoutMinutes);
                    }
                }
                break;
            case "Receptionist":
                var receptionist = await _db.Receptionists.FindAsync(new object[] { userId }, cancellationToken);
                if (receptionist is not null)
                {
                    receptionist.FailedLoginAttempts++;
                    if (receptionist.FailedLoginAttempts >= MaxFailedAttempts)
                    {
                        receptionist.LockoutEndUtc = DateTime.UtcNow.AddMinutes(LockoutMinutes);
                    }
                }
                break;
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetLockoutAsync(string userType, int userId, CancellationToken cancellationToken)
    {
        switch (userType)
        {
            case "Resident":
                var resident = await _db.Residents.FindAsync(new object[] { userId }, cancellationToken);
                if (resident is not null)
                {
                    resident.FailedLoginAttempts = 0;
                    resident.LockoutEndUtc = null;
                }
                break;
            case "Admin":
                var admin = await _db.AdminUsers.FindAsync(new object[] { userId }, cancellationToken);
                if (admin is not null)
                {
                    admin.FailedLoginAttempts = 0;
                    admin.LockoutEndUtc = null;
                }
                break;
            case "Receptionist":
                var receptionist = await _db.Receptionists.FindAsync(new object[] { userId }, cancellationToken);
                if (receptionist is not null)
                {
                    receptionist.FailedLoginAttempts = 0;
                    receptionist.LockoutEndUtc = null;
                }
                break;
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    public TimeSpan? GetRemainingLockoutTime(DateTime? lockoutEndUtc)
    {
        if (!lockoutEndUtc.HasValue || lockoutEndUtc.Value <= DateTime.UtcNow)
            return null;
        return lockoutEndUtc.Value - DateTime.UtcNow;
    }
}
