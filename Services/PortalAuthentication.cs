using Microsoft.EntityFrameworkCore;
using ResidentPortal.Data;
using ResidentPortal.Models;
using ResidentPortal.Security;

namespace ResidentPortal.Services;

public sealed class PortalAuthentication
{
    private readonly ApplicationDbContext _db;
    private readonly PasswordService _passwords;

    public PortalAuthentication(ApplicationDbContext db, PasswordService passwords)
    {
        _db = db;
        _passwords = passwords;
    }

    public Task<Resident?> ValidateResidentAsync(string username, string password, CancellationToken cancellationToken) =>
        ValidateAsync(
            username,
            password,
            (name, token) => _db.Residents.SingleOrDefaultAsync(r => r.Username == name, token),
            resident => resident.PasswordHash,
            resident => resident.IsActive,
            cancellationToken);

    public Task<AdminUser?> ValidateAdminAsync(string username, string password, CancellationToken cancellationToken) =>
        ValidateAsync(
            username,
            password,
            (name, token) => _db.AdminUsers.SingleOrDefaultAsync(a => a.Username == name, token),
            admin => admin.PasswordHash,
            admin => admin.IsActive,
            cancellationToken);

    public Task<Receptionist?> ValidateReceptionistAsync(string username, string password, CancellationToken cancellationToken) =>
        ValidateAsync(
            username,
            password,
            (name, token) => _db.Receptionists.SingleOrDefaultAsync(r => r.Username == name, token),
            receptionist => receptionist.PasswordHash,
            receptionist => receptionist.IsActive,
            cancellationToken);

    private async Task<TUser?> ValidateAsync<TUser>(
        string username,
        string password,
        Func<string, CancellationToken, Task<TUser?>> find,
        Func<TUser, string> passwordHash,
        Func<TUser, bool> isActive,
        CancellationToken cancellationToken)
        where TUser : class
    {
        var normalized = username.Trim();
        var account = await find(normalized, cancellationToken);
        if (account is null)
        {
            _passwords.VerifyDummy(password);
            return null;
        }

        if (!_passwords.Verify(passwordHash(account), password) || !isActive(account))
            return null;

        return account;
    }
}
