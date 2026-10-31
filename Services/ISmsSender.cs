namespace ResidentPortal.Services;

public interface ISmsSender
{
    Task SendAsync(string toPhone, string message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Placeholder SMS sender: when SMS is enabled and configured it logs only.
/// Wire a real provider later without changing call sites.
/// </summary>
public sealed class LoggingSmsSender : ISmsSender
{
    private readonly SmsSettingsService _settings;
    private readonly ILogger<LoggingSmsSender> _logger;

    public LoggingSmsSender(SmsSettingsService settings, ILogger<LoggingSmsSender> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task SendAsync(string toPhone, string message, CancellationToken cancellationToken = default)
    {
        var settings = await _settings.GetAsync(cancellationToken);
        if (!settings.Enabled)
            return;

        if (!settings.IsReady)
        {
            _logger.LogWarning(
                "SMS skipped: enabled but missing provider, API URL, API key, or sender ID.");
            return;
        }

        if (string.IsNullOrWhiteSpace(toPhone) || string.IsNullOrWhiteSpace(message))
            return;

        _logger.LogInformation(
            "SMS placeholder ({Provider}) to {Phone} from {Sender}: {Message}",
            settings.ProviderName,
            toPhone.Trim(),
            settings.SenderId,
            message.Trim());
        await Task.CompletedTask;
    }
}
