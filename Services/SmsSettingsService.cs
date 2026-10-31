using Microsoft.AspNetCore.DataProtection;
using ResidentPortal.Models;

namespace ResidentPortal.Services;

public sealed class SmsSettings
{
    public bool Enabled { get; init; }

    public string? ProviderName { get; init; }

    public string? ApiBaseUrl { get; init; }

    public string? ApiKeyProtected { get; init; }

    public string? SenderId { get; init; }

    public string? ExtraSettings { get; init; }

    public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKeyProtected);

    public bool IsReady =>
        Enabled
        && !string.IsNullOrWhiteSpace(ProviderName)
        && !string.IsNullOrWhiteSpace(ApiBaseUrl)
        && HasApiKey
        && !string.IsNullOrWhiteSpace(SenderId);
}

public sealed class SmsSettingsService
{
    private const string ProtectorPurpose = "ResidentPortal.SmsApiKey.v1";
    private readonly AppSettingsService _settings;
    private readonly IDataProtector _protector;

    public SmsSettingsService(AppSettingsService settings, IDataProtectionProvider dataProtection)
    {
        _settings = settings;
        _protector = dataProtection.CreateProtector(ProtectorPurpose);
    }

    public async Task<SmsSettings> GetAsync(CancellationToken ct = default)
    {
        var enabledRaw = await _settings.GetSettingAsync(AppSettingKeys.SmsEnabled, ct);
        return new SmsSettings
        {
            Enabled = string.Equals(enabledRaw, "true", StringComparison.OrdinalIgnoreCase),
            ProviderName = Normalize(await _settings.GetSettingAsync(AppSettingKeys.SmsProviderName, ct)),
            ApiBaseUrl = Normalize(await _settings.GetSettingAsync(AppSettingKeys.SmsApiBaseUrl, ct)),
            ApiKeyProtected = Normalize(await _settings.GetSettingAsync(AppSettingKeys.SmsApiKeyProtected, ct)),
            SenderId = Normalize(await _settings.GetSettingAsync(AppSettingKeys.SmsSenderId, ct)),
            ExtraSettings = Normalize(await _settings.GetSettingAsync(AppSettingKeys.SmsExtraSettings, ct))
        };
    }

    public async Task SaveAsync(AdminConfigurationViewModel model, CancellationToken ct = default)
    {
        await _settings.SetSettingAsync(
            AppSettingKeys.SmsEnabled,
            model.SmsNotificationsEnabled ? "true" : "false",
            ct);
        await _settings.SetSettingAsync(AppSettingKeys.SmsProviderName, Normalize(model.SmsProviderName) ?? string.Empty, ct);
        await _settings.SetSettingAsync(AppSettingKeys.SmsApiBaseUrl, Normalize(model.SmsApiBaseUrl) ?? string.Empty, ct);
        await _settings.SetSettingAsync(AppSettingKeys.SmsSenderId, Normalize(model.SmsSenderId) ?? string.Empty, ct);
        await _settings.SetSettingAsync(AppSettingKeys.SmsExtraSettings, Normalize(model.SmsExtraSettings) ?? string.Empty, ct);

        if (!string.IsNullOrWhiteSpace(model.SmsApiKey))
        {
            var protectedKey = _protector.Protect(model.SmsApiKey.Trim());
            await _settings.SetSettingAsync(AppSettingKeys.SmsApiKeyProtected, protectedKey, ct);
        }
    }

    public string? UnprotectApiKey(SmsSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKeyProtected))
            return null;

        try
        {
            return _protector.Unprotect(settings.ApiKeyProtected);
        }
        catch
        {
            return null;
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
