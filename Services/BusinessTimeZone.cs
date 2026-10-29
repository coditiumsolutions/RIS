namespace ResidentPortal.Services;

public sealed class BusinessTimeZone
{
    private readonly TimeZoneInfo _timeZone;

    public BusinessTimeZone()
    {
        try
        {
            _timeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Karachi");
        }
        catch (TimeZoneNotFoundException)
        {
            _timeZone = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
        }
    }

    public TimeZoneInfo TimeZone => _timeZone;

    public string DisplayAbbreviation => "PKT";

    public DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _timeZone);

    public DateOnly Today => DateOnly.FromDateTime(Now);

    public DateTime ConvertFromUtc(DateTime utcDateTime)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, _timeZone);
    }

    public DateTime? ConvertFromUtc(DateTime? utcDateTime)
    {
        return utcDateTime.HasValue
            ? TimeZoneInfo.ConvertTimeFromUtc(utcDateTime.Value, _timeZone)
            : null;
    }

    public DateTime ConvertToUtc(DateTime localDateTime)
    {
        return TimeZoneInfo.ConvertTimeToUtc(localDateTime, _timeZone);
    }

    public string FormatDateTime(DateTime utcDateTime, string format = "dd MMM yyyy, HH:mm")
    {
        var local = ConvertFromUtc(utcDateTime);
        return $"{local.ToString(format)} {DisplayAbbreviation}";
    }

    public string? FormatDateTime(DateTime? utcDateTime, string format = "dd MMM yyyy, HH:mm")
    {
        if (!utcDateTime.HasValue)
            return null;
        return FormatDateTime(utcDateTime.Value, format);
    }
}
