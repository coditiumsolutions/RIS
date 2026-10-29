using Microsoft.EntityFrameworkCore;
using ResidentPortal.Data;
using ResidentPortal.Security;

namespace ResidentPortal.Services;

public enum PasswordChangeStatus
{
    Success,
    AccountUnavailable,
    CurrentPasswordIncorrect,
    SameAsCurrent
}

public sealed class PasswordChangeService
{
    private readonly ApplicationDbContext _db;
    private readonly PasswordService _passwords;
    private readonly AuditLogger _audit;

    public PasswordChangeService(ApplicationDbContext db, PasswordService passwords, AuditLogger audit)
    {
        _db = db;
        _passwords = passwords;
        _audit = audit;
    }

    public async Task<PasswordChangeStatus> ChangeResidentPasswordAsync(
        int userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken)
    {
        var resident = await _db.Residents
            .SingleOrDefaultAsync(r => r.Uid == userId && r.IsActive, cancellationToken);

        if (resident is null)
            return PasswordChangeStatus.AccountUnavailable;

        if (!_passwords.Verify(resident.PasswordHash, currentPassword))
            return PasswordChangeStatus.CurrentPasswordIncorrect;

        if (_passwords.Verify(resident.PasswordHash, newPassword))
            return PasswordChangeStatus.SameAsCurrent;

        resident.PasswordHash = _passwords.Hash(newPassword);
        resident.MustChangePassword = false;
        resident.SecurityStamp = Guid.NewGuid().ToString("D").ToLowerInvariant();
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("Resident", resident.Uid, "Password changed", cancellationToken);
        return PasswordChangeStatus.Success;
    }

    public async Task<PasswordChangeStatus> ChangeAdminPasswordAsync(
        int userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken)
    {
        var admin = await _db.AdminUsers
            .SingleOrDefaultAsync(a => a.Uid == userId && a.IsActive, cancellationToken);

        if (admin is null)
            return PasswordChangeStatus.AccountUnavailable;

        if (!_passwords.Verify(admin.PasswordHash, currentPassword))
            return PasswordChangeStatus.CurrentPasswordIncorrect;

        if (_passwords.Verify(admin.PasswordHash, newPassword))
            return PasswordChangeStatus.SameAsCurrent;

        admin.PasswordHash = _passwords.Hash(newPassword);
        admin.MustChangePassword = false;
        admin.SecurityStamp = Guid.NewGuid().ToString("D").ToLowerInvariant();
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("Admin", admin.Uid, "Password changed", cancellationToken);
        return PasswordChangeStatus.Success;
    }

    public async Task<PasswordChangeStatus> ChangeReceptionistPasswordAsync(
        int userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken)
    {
        var receptionist = await _db.Receptionists
            .SingleOrDefaultAsync(r => r.Uid == userId && r.IsActive, cancellationToken);

        if (receptionist is null)
            return PasswordChangeStatus.AccountUnavailable;

        if (!_passwords.Verify(receptionist.PasswordHash, currentPassword))
            return PasswordChangeStatus.CurrentPasswordIncorrect;

        if (_passwords.Verify(receptionist.PasswordHash, newPassword))
            return PasswordChangeStatus.SameAsCurrent;

        receptionist.PasswordHash = _passwords.Hash(newPassword);
        receptionist.MustChangePassword = false;
        receptionist.SecurityStamp = Guid.NewGuid().ToString("D").ToLowerInvariant();
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("Receptionist", receptionist.Uid, "Password changed", cancellationToken);
        return PasswordChangeStatus.Success;
    }
}
