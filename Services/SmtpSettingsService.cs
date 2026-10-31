using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using ResidentPortal.Data;
using ResidentPortal.Models;

namespace ResidentPortal.Services;

public class SmtpSettingsService
{
    private const string ProtectorPurpose = "ResidentPortal.SmtpPassword.v1";
    private readonly ApplicationDbContext _db;
    private readonly IDataProtector _protector;

    public SmtpSettingsService(ApplicationDbContext db, IDataProtectionProvider dataProtection)
    {
        _db = db;
        _protector = dataProtection.CreateProtector(ProtectorPurpose);
    }

    public async Task<SmtpSettings> GetOrCreateAsync(CancellationToken ct)
    {
        var row = await _db.SmtpSettings.OrderBy(s => s.Uid).FirstOrDefaultAsync(ct);
        if (row is not null)
            return row;

        row = new SmtpSettings
        {
            Port = 587,
            UseSsl = true,
            EmailNotificationsEnabled = false,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _db.SmtpSettings.Add(row);
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task SaveAsync(AdminConfigurationViewModel model, CancellationToken ct)
    {
        var row = await GetOrCreateAsync(ct);
        row.Host = Normalize(model.SmtpHost);
        row.Port = model.SmtpPort;
        row.UseSsl = model.SmtpUseSsl;
        row.Username = Normalize(model.SmtpUsername);
        row.FromEmail = Normalize(model.FromEmail);
        row.FromName = Normalize(model.FromName);
        row.EmailNotificationsEnabled = model.EmailNotificationsEnabled;
        row.UpdatedAtUtc = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(model.SmtpPassword))
            row.PasswordProtected = _protector.Protect(model.SmtpPassword);

        await _db.SaveChangesAsync(ct);
    }

    public string? UnprotectPassword(SmtpSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.PasswordProtected))
            return null;
        try
        {
            return _protector.Unprotect(settings.PasswordProtected);
        }
        catch
        {
            return null;
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
