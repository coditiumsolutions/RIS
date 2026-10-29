using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace ResidentPortal.Services;

/// <summary>
/// Counts only failed login attempts, partitioned by portal + client IP.
/// Successful logins never consume quota.
/// </summary>
public sealed class LoginFailureRateLimiter
{
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _config;
    private static readonly ConcurrentDictionary<string, object> KeyLocks = new(StringComparer.Ordinal);

    public LoginFailureRateLimiter(IMemoryCache cache, IConfiguration config)
    {
        _cache = cache;
        _config = config;
    }

    public int MaxFailedAttempts => _config.GetValue("LoginRateLimit:MaxFailedAttempts", 20);
    public int WindowMinutes => _config.GetValue("LoginRateLimit:WindowMinutes", 15);

    public bool IsLimited(string portal, string? ipAddress)
    {
        var key = BuildKey(portal, ipAddress);
        return _cache.TryGetValue(key, out AttemptWindow? window)
            && window is not null
            && window.Count >= MaxFailedAttempts;
    }

    public void RecordFailure(string portal, string? ipAddress)
    {
        var key = BuildKey(portal, ipAddress);
        var gate = KeyLocks.GetOrAdd(key, static _ => new object());
        lock (gate)
        {
            var now = DateTimeOffset.UtcNow;
            if (!_cache.TryGetValue(key, out AttemptWindow? window)
                || window is null
                || window.WindowStart.AddMinutes(WindowMinutes) <= now)
            {
                window = new AttemptWindow(now, 0);
            }

            window = window with { Count = window.Count + 1 };
            var remaining = window.WindowStart.AddMinutes(WindowMinutes) - now;
            if (remaining <= TimeSpan.Zero)
                remaining = TimeSpan.FromMinutes(WindowMinutes);

            _cache.Set(key, window, remaining);
        }
    }

    private static string BuildKey(string portal, string? ipAddress) =>
        $"login-fail:{portal}:{ipAddress ?? "unknown"}";

    private sealed record AttemptWindow(DateTimeOffset WindowStart, int Count);
}
