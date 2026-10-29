using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ResidentPortal.Data;
using ResidentPortal.Models;

namespace ResidentPortal.Services;

public sealed class AppSettingsService
{
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public AppSettingsService(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<bool> IsVisitorApprovalRequiredAsync(CancellationToken cancellationToken = default)
    {
        var value = await GetSettingAsync(AppSettingKeys.VisitorApprovalRequired, cancellationToken);
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<int> GetMaxGuestsPerVisitAsync(CancellationToken cancellationToken = default)
    {
        var value = await GetSettingAsync(AppSettingKeys.MaxGuestsPerVisit, cancellationToken);
        return int.TryParse(value, out var max) ? max : 10;
    }

    public async Task<string?> GetSettingAsync(string key, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"AppSetting_{key}";
        if (_cache.TryGetValue(cacheKey, out string? cachedValue))
            return cachedValue;

        var setting = await _db.AppSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.SettingKey == key, cancellationToken);

        var value = setting?.SettingValue;
        _cache.Set(cacheKey, value, CacheDuration);
        return value;
    }

    public async Task SetSettingAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var setting = await _db.AppSettings.SingleOrDefaultAsync(s => s.SettingKey == key, cancellationToken);
        if (setting is null)
        {
            setting = new AppSetting { SettingKey = key, SettingValue = value, UpdatedAt = DateTime.UtcNow };
            _db.AppSettings.Add(setting);
        }
        else
        {
            setting.SettingValue = value;
            setting.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        _cache.Remove($"AppSetting_{key}");
    }
}
