using Microsoft.EntityFrameworkCore;
using ResidentPortal.Data;
using ResidentPortal.Models;
using ResidentPortal.Security;

namespace ResidentPortal.Services;

public enum LoginResult
{
    Success,
    InvalidCredentials,
    AccountLocked,
    AccountInactive
}

public sealed class PortalAuthentication
{
    private readonly ApplicationDbContext _db;
    private readonly PasswordService _passwords;
    private readonly IConfiguration _config;

    public PortalAuthentication(ApplicationDbContext db, PasswordService passwords, IConfiguration config)
    {
        _db = db;
        _passwords = passwords;
        _config = config;
    }

    private int MaxFailedAttempts => _config.GetValue("Lockout:MaxFailedAttempts", 5);
    private int LockoutMinutes => _config.GetValue("Lockout:LockoutMinutes", 15);

    public async Task<(LoginResult Result, Resident? User, TimeSpan? LockoutRemaining)> ValidateResidentAsync(
        string username, string password, CancellationToken cancellationToken)
    {
        var normalized = username.Trim();
        var account = await _db.Residents.SingleOrDefaultAsync(r => r.Username == normalized, cancellationToken);

        if (account is null)
        {
            _passwords.VerifyDummy(password);
            return (LoginResult.InvalidCredentials, null, null);
        }

        if (account.LockoutEndUtc.HasValue && account.LockoutEndUtc.Value > DateTime.UtcNow)
        {
            var remaining = account.LockoutEndUtc.Value - DateTime.UtcNow;
            return (LoginResult.AccountLocked, null, remaining);
        }

        if (!account.IsActive)
            return (LoginResult.AccountInactive, null, null);

        if (!_passwords.Verify(account.PasswordHash, password))
        {
            account.FailedLoginAttempts++;
            if (account.FailedLoginAttempts >= MaxFailedAttempts)
            {
                account.LockoutEndUtc = DateTime.UtcNow.AddMinutes(LockoutMinutes);
                await _db.SaveChangesAsync(cancellationToken);
                return (LoginResult.AccountLocked, null, TimeSpan.FromMinutes(LockoutMinutes));
            }
            await _db.SaveChangesAsync(cancellationToken);
            return (LoginResult.InvalidCredentials, null, null);
        }

        account.FailedLoginAttempts = 0;
        account.LockoutEndUtc = null;
        if (string.IsNullOrWhiteSpace(account.SecurityStamp))
            account.SecurityStamp = Guid.NewGuid().ToString("D").ToLowerInvariant();
        await _db.SaveChangesAsync(cancellationToken);

        return (LoginResult.Success, account, null);
    }

    public async Task<(LoginResult Result, AdminUser? User, TimeSpan? LockoutRemaining)> ValidateAdminAsync(
        string username, string password, CancellationToken cancellationToken)
    {
        var normalized = username.Trim();
        var account = await _db.AdminUsers.SingleOrDefaultAsync(a => a.Username == normalized, cancellationToken);

        if (account is null)
        {
            _passwords.VerifyDummy(password);
            return (LoginResult.InvalidCredentials, null, null);
        }

        if (account.LockoutEndUtc.HasValue && account.LockoutEndUtc.Value > DateTime.UtcNow)
        {
            var remaining = account.LockoutEndUtc.Value - DateTime.UtcNow;
            return (LoginResult.AccountLocked, null, remaining);
        }

        if (!account.IsActive)
            return (LoginResult.AccountInactive, null, null);

        if (!_passwords.Verify(account.PasswordHash, password))
        {
            account.FailedLoginAttempts++;
            if (account.FailedLoginAttempts >= MaxFailedAttempts)
            {
                account.LockoutEndUtc = DateTime.UtcNow.AddMinutes(LockoutMinutes);
                await _db.SaveChangesAsync(cancellationToken);
                return (LoginResult.AccountLocked, null, TimeSpan.FromMinutes(LockoutMinutes));
            }
            await _db.SaveChangesAsync(cancellationToken);
            return (LoginResult.InvalidCredentials, null, null);
        }

        account.FailedLoginAttempts = 0;
        account.LockoutEndUtc = null;
        if (string.IsNullOrWhiteSpace(account.SecurityStamp))
            account.SecurityStamp = Guid.NewGuid().ToString("D").ToLowerInvariant();
        await _db.SaveChangesAsync(cancellationToken);

        return (LoginResult.Success, account, null);
    }

    public async Task<(LoginResult Result, Receptionist? User, TimeSpan? LockoutRemaining)> ValidateReceptionistAsync(
        string username, string password, CancellationToken cancellationToken)
    {
        var normalized = username.Trim();
        var account = await _db.Receptionists.SingleOrDefaultAsync(r => r.Username == normalized, cancellationToken);

        if (account is null)
        {
            _passwords.VerifyDummy(password);
            return (LoginResult.InvalidCredentials, null, null);
        }

        if (account.LockoutEndUtc.HasValue && account.LockoutEndUtc.Value > DateTime.UtcNow)
        {
            var remaining = account.LockoutEndUtc.Value - DateTime.UtcNow;
            return (LoginResult.AccountLocked, null, remaining);
        }

        if (!account.IsActive)
            return (LoginResult.AccountInactive, null, null);

        if (!_passwords.Verify(account.PasswordHash, password))
        {
            account.FailedLoginAttempts++;
            if (account.FailedLoginAttempts >= MaxFailedAttempts)
            {
                account.LockoutEndUtc = DateTime.UtcNow.AddMinutes(LockoutMinutes);
                await _db.SaveChangesAsync(cancellationToken);
                return (LoginResult.AccountLocked, null, TimeSpan.FromMinutes(LockoutMinutes));
            }
            await _db.SaveChangesAsync(cancellationToken);
            return (LoginResult.InvalidCredentials, null, null);
        }

        account.FailedLoginAttempts = 0;
        account.LockoutEndUtc = null;
        if (string.IsNullOrWhiteSpace(account.SecurityStamp))
            account.SecurityStamp = Guid.NewGuid().ToString("D").ToLowerInvariant();
        await _db.SaveChangesAsync(cancellationToken);

        return (LoginResult.Success, account, null);
    }
}
