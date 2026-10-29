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
}
