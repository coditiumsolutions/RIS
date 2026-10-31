namespace ResidentPortal.Models;

public class AppSetting
{
    public int Uid { get; set; }

    public string SettingKey { get; set; } = string.Empty;

    public string SettingValue { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public static class AppSettingKeys
{
    public const string VisitorApprovalRequired = "VisitorApprovalRequired";
    public const string MaxGuestsPerVisit = "MaxGuestsPerVisit";
    public const string ComplaintSlaUrgentHours = "ComplaintSlaUrgentHours";
    public const string ComplaintSlaNormalHours = "ComplaintSlaNormalHours";
    public const string ComplaintSlaLowHours = "ComplaintSlaLowHours";

    public const string SmsEnabled = "Sms.Enabled";
    public const string SmsProviderName = "Sms.ProviderName";
    public const string SmsApiBaseUrl = "Sms.ApiBaseUrl";
    public const string SmsApiKeyProtected = "Sms.ApiKeyProtected";
    public const string SmsSenderId = "Sms.SenderId";
    public const string SmsExtraSettings = "Sms.ExtraSettings";
}
