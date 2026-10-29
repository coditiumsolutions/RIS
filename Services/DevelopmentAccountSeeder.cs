using Microsoft.EntityFrameworkCore;
using ResidentPortal.Data;
using ResidentPortal.Models;
using ResidentPortal.Security;

namespace ResidentPortal.Services;

public sealed class DevelopmentAccountSeeder
{
    private readonly ApplicationDbContext _db;
    private readonly PasswordService _passwords;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DevelopmentAccountSeeder> _logger;

    public DevelopmentAccountSeeder(
        ApplicationDbContext db,
        PasswordService passwords,
        IHostEnvironment environment,
        IConfiguration configuration,
        ILogger<DevelopmentAccountSeeder> logger)
    {
        _db = db;
        _passwords = passwords;
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment() || !_configuration.GetValue("Seed:Enabled", false))
            return;

        await SeedResidentAsync(cancellationToken);
        await SeedAdminAsync(cancellationToken);
    }

    private async Task SeedResidentAsync(CancellationToken cancellationToken)
    {
        var username = _configuration["Seed:ResidentUsername"]?.Trim();
        var password = _configuration["Seed:ResidentPassword"];
        if (!TryReadCredential("resident", username, password, out var safeUsername, out var safePassword))
            return;

        if (await _db.Residents.AnyAsync(r => r.Username == safeUsername, cancellationToken))
            return;

        _db.Residents.Add(new Resident
        {
            ResidentName = _configuration["Seed:ResidentName"]?.Trim() ?? "Resident",
            Username = safeUsername,
            PasswordHash = _passwords.Hash(safePassword),
            Email = _configuration["Seed:ResidentEmail"]?.Trim(),
            Mobile = _configuration["Seed:ResidentMobile"]?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Created the development resident account.");
    }

    private async Task SeedAdminAsync(CancellationToken cancellationToken)
    {
        var username = _configuration["Seed:AdminUsername"]?.Trim();
        var password = _configuration["Seed:AdminPassword"];
        if (!TryReadCredential("administrator", username, password, out var safeUsername, out var safePassword))
            return;

        if (await _db.AdminUsers.AnyAsync(a => a.Username == safeUsername, cancellationToken))
            return;

        _db.AdminUsers.Add(new AdminUser
        {
            FullName = _configuration["Seed:AdminName"]?.Trim() ?? "Administrator",
            Username = safeUsername,
            PasswordHash = _passwords.Hash(safePassword),
            Email = _configuration["Seed:AdminEmail"]?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Created the development administrator account.");
    }

    private bool TryReadCredential(string accountLabel, string? username, string? password, out string safeUsername, out string safePassword)
    {
        safeUsername = username ?? string.Empty;
        safePassword = password ?? string.Empty;

        if (string.IsNullOrWhiteSpace(safeUsername) || string.IsNullOrWhiteSpace(safePassword))
        {
            _logger.LogWarning(
                "Skipped the development {AccountLabel} account because its username or password is not configured in user secrets.",
                accountLabel);
            return false;
        }

        if (safePassword.Length < 8)
        {
            _logger.LogWarning(
                "Skipped the development {AccountLabel} account because the configured password is shorter than 8 characters.",
                accountLabel);
            return false;
        }

        return true;
    }
}
