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
    AccountInactive,
    AmbiguousUsername
}

/// <summary>Normalized staff account returned by <see cref="PortalAuthentication.ValidateStaffAsync"/>.</summary>
public sealed class StaffAuthUser
{
    public required string UserType { get; init; }

    public required int Uid { get; init; }

    public required string FullName { get; init; }

    public required string Username { get; init; }

    public string? Email { get; init; }

    public required string SecurityStamp { get; init; }

    public bool MustChangePassword { get; init; }
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

    public Task<(LoginResult Result, Resident? User, TimeSpan? LockoutRemaining)> ValidateResidentAsync(
        string username, string password, CancellationToken cancellationToken) =>
        ValidateAsync(
            username,
            password,
            (name, token) => _db.Residents.SingleOrDefaultAsync(r => r.Username == name, token),
            cancellationToken);

    public Task<(LoginResult Result, AdminUser? User, TimeSpan? LockoutRemaining)> ValidateAdminAsync(
        string username, string password, CancellationToken cancellationToken) =>
        ValidateAsync(
            username,
            password,
            (name, token) => _db.AdminUsers.SingleOrDefaultAsync(a => a.Username == name, token),
            cancellationToken);

    public Task<(LoginResult Result, Receptionist? User, TimeSpan? LockoutRemaining)> ValidateReceptionistAsync(
        string username, string password, CancellationToken cancellationToken) =>
        ValidateAsync(
            username,
            password,
            (name, token) => _db.Receptionists.SingleOrDefaultAsync(r => r.Username == name, token),
            cancellationToken);

    /// <summary>
    /// Unified Staff login: looks up AdminUsers then Receptionists.
    /// If the same username exists in both tables, returns <see cref="LoginResult.AmbiguousUsername"/>.
    /// </summary>
    public async Task<(LoginResult Result, StaffAuthUser? User, TimeSpan? LockoutRemaining)> ValidateStaffAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        var normalized = username.Trim();
        var admin = await _db.AdminUsers.SingleOrDefaultAsync(a => a.Username == normalized, cancellationToken);
        var receptionist = await _db.Receptionists.SingleOrDefaultAsync(r => r.Username == normalized, cancellationToken);

        if (admin is not null && receptionist is not null)
        {
            _passwords.VerifyDummy(password);
            return (LoginResult.AmbiguousUsername, null, null);
        }

        if (admin is null && receptionist is null)
        {
            _passwords.VerifyDummy(password);
            return (LoginResult.InvalidCredentials, null, null);
        }

        if (admin is not null)
        {
            var (result, user, lockout) = await ValidateFoundAccountAsync(admin, password, cancellationToken);
            if (result != LoginResult.Success || user is null)
                return (result, null, lockout);

            return (LoginResult.Success, ToStaffAuthUser(user), null);
        }

        {
            var (result, user, lockout) = await ValidateFoundAccountAsync(receptionist!, password, cancellationToken);
            if (result != LoginResult.Success || user is null)
                return (result, null, lockout);

            return (LoginResult.Success, ToStaffAuthUser(user), null);
        }
    }

    private static StaffAuthUser ToStaffAuthUser(AdminUser admin) => new()
    {
        UserType = PortalRoles.Admin,
        Uid = admin.Uid,
        FullName = admin.FullName,
        Username = admin.Username,
        Email = admin.Email,
        SecurityStamp = admin.SecurityStamp ?? string.Empty,
        MustChangePassword = admin.MustChangePassword
    };

    private static StaffAuthUser ToStaffAuthUser(Receptionist receptionist) => new()
    {
        UserType = PortalRoles.Receptionist,
        Uid = receptionist.Uid,
        FullName = receptionist.FullName,
        Username = receptionist.Username,
        Email = receptionist.Email,
        SecurityStamp = receptionist.SecurityStamp ?? string.Empty,
        MustChangePassword = receptionist.MustChangePassword
    };

    private async Task<(LoginResult Result, TUser? User, TimeSpan? LockoutRemaining)> ValidateFoundAccountAsync<TUser>(
        TUser account,
        string password,
        CancellationToken cancellationToken)
        where TUser : class
    {
        var passwordHash = GetPasswordHash(account);
        if (!_passwords.Verify(passwordHash, password))
        {
            await RecordFailedAttemptAsync(account, cancellationToken);
            return (LoginResult.InvalidCredentials, null, null);
        }

        var lockoutEnd = GetLockoutEnd(account);
        if (lockoutEnd.HasValue && lockoutEnd.Value > DateTime.UtcNow)
            return (LoginResult.AccountLocked, null, lockoutEnd.Value - DateTime.UtcNow);

        if (!GetIsActive(account))
            return (LoginResult.AccountInactive, null, null);

        ResetLockout(account);
        EnsureSecurityStamp(account);
        await _db.SaveChangesAsync(cancellationToken);
        return (LoginResult.Success, account, null);
    }

    private async Task<(LoginResult Result, TUser? User, TimeSpan? LockoutRemaining)> ValidateAsync<TUser>(
        string username,
        string password,
        Func<string, CancellationToken, Task<TUser?>> find,
        CancellationToken cancellationToken)
        where TUser : class
    {
        var normalized = username.Trim();
        var account = await find(normalized, cancellationToken);
        if (account is null)
        {
            _passwords.VerifyDummy(password);
            return (LoginResult.InvalidCredentials, null, null);
        }

        var passwordHash = GetPasswordHash(account);
        if (!_passwords.Verify(passwordHash, password))
        {
            await RecordFailedAttemptAsync(account, cancellationToken);
            return (LoginResult.InvalidCredentials, null, null);
        }

        // Correct password: only then reveal lockout / inactive status (QA-052)
        var lockoutEnd = GetLockoutEnd(account);
        if (lockoutEnd.HasValue && lockoutEnd.Value > DateTime.UtcNow)
            return (LoginResult.AccountLocked, null, lockoutEnd.Value - DateTime.UtcNow);

        if (!GetIsActive(account))
            return (LoginResult.AccountInactive, null, null);

        ResetLockout(account);
        EnsureSecurityStamp(account);
        await _db.SaveChangesAsync(cancellationToken);
        return (LoginResult.Success, account, null);
    }

    private async Task RecordFailedAttemptAsync<TUser>(TUser account, CancellationToken cancellationToken)
        where TUser : class
    {
        var lockoutEnd = GetLockoutEnd(account);
        if (lockoutEnd.HasValue && lockoutEnd.Value > DateTime.UtcNow)
            return;

        IncrementFailedAttempts(account);
        if (GetFailedAttempts(account) >= MaxFailedAttempts)
            SetLockoutEnd(account, DateTime.UtcNow.AddMinutes(LockoutMinutes));

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string GetPasswordHash<TUser>(TUser account) => account switch
    {
        Resident r => r.PasswordHash,
        AdminUser a => a.PasswordHash,
        Receptionist r => r.PasswordHash,
        _ => string.Empty
    };

    private static bool GetIsActive<TUser>(TUser account) => account switch
    {
        Resident r => r.IsActive,
        AdminUser a => a.IsActive,
        Receptionist r => r.IsActive,
        _ => false
    };

    private static DateTime? GetLockoutEnd<TUser>(TUser account) => account switch
    {
        Resident r => r.LockoutEndUtc,
        AdminUser a => a.LockoutEndUtc,
        Receptionist r => r.LockoutEndUtc,
        _ => null
    };

    private static int GetFailedAttempts<TUser>(TUser account) => account switch
    {
        Resident r => r.FailedLoginAttempts,
        AdminUser a => a.FailedLoginAttempts,
        Receptionist r => r.FailedLoginAttempts,
        _ => 0
    };

    private static void IncrementFailedAttempts<TUser>(TUser account)
    {
        switch (account)
        {
            case Resident r: r.FailedLoginAttempts++; break;
            case AdminUser a: a.FailedLoginAttempts++; break;
            case Receptionist r: r.FailedLoginAttempts++; break;
        }
    }

    private static void SetLockoutEnd<TUser>(TUser account, DateTime endUtc)
    {
        switch (account)
        {
            case Resident r: r.LockoutEndUtc = endUtc; break;
            case AdminUser a: a.LockoutEndUtc = endUtc; break;
            case Receptionist r: r.LockoutEndUtc = endUtc; break;
        }
    }

    private static void ResetLockout<TUser>(TUser account)
    {
        switch (account)
        {
            case Resident r:
                r.FailedLoginAttempts = 0;
                r.LockoutEndUtc = null;
                break;
            case AdminUser a:
                a.FailedLoginAttempts = 0;
                a.LockoutEndUtc = null;
                break;
            case Receptionist r:
                r.FailedLoginAttempts = 0;
                r.LockoutEndUtc = null;
                break;
        }
    }

    private static void EnsureSecurityStamp<TUser>(TUser account)
    {
        switch (account)
        {
            case Resident r when string.IsNullOrWhiteSpace(r.SecurityStamp):
                r.SecurityStamp = Guid.NewGuid().ToString("D").ToLowerInvariant();
                break;
            case AdminUser a when string.IsNullOrWhiteSpace(a.SecurityStamp):
                a.SecurityStamp = Guid.NewGuid().ToString("D").ToLowerInvariant();
                break;
            case Receptionist r when string.IsNullOrWhiteSpace(r.SecurityStamp):
                r.SecurityStamp = Guid.NewGuid().ToString("D").ToLowerInvariant();
                break;
        }
    }
}
