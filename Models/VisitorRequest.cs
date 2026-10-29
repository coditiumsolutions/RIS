namespace ResidentPortal.Models;

public class VisitorRequest
{
    public int Uid { get; set; }

    public int ResidentId { get; set; }

    public string VisitorName { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Purpose { get; set; } = string.Empty;

    public string Duration { get; set; } = string.Empty;

    public DateOnly ArrivalDate { get; set; }

    public int NumberOfGuests { get; set; } = 1;

    public string? CNIC { get; set; }

    public string? VehicleRegistrationNo { get; set; }

    public string Status { get; set; } = VisitorStatus.Pending;

    public bool IsWalkIn { get; set; }

    public DateTime? CheckedInAt { get; set; }

    public DateTime? CheckedOutAt { get; set; }

    public int? CheckedInByUserId { get; set; }

    public string? CheckedInByUserType { get; set; }

    public int? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public int? DeclinedByUserId { get; set; }

    public DateTime? DeclinedAt { get; set; }

    public string? DeclineReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Resident Resident { get; set; } = null!;
}

public static class VisitorStatus
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Declined = "Declined";
    public const string CheckedIn = "CheckedIn";
    public const string CheckedOut = "CheckedOut";
    public const string Cancelled = "Cancelled";
}

public static class VisitorDuration
{
    public const string LessThan6Hours = "Less than 6 hours";
    public const string OneDay = "1 Day";
    public const string TwoDays = "2 Days";
    public const string ThreeDays = "3 Days";
    public const string SevenDays = "7 Days";
    public const string FourteenDays = "14 Days";

    public static readonly IReadOnlyList<string> All = new[]
    {
        LessThan6Hours,
        OneDay,
        TwoDays,
        ThreeDays,
        SevenDays,
        FourteenDays
    };

    public static bool IsAllowed(string? duration) =>
        !string.IsNullOrWhiteSpace(duration) && All.Contains(duration);

    public static int GetStayDays(string duration) => duration switch
    {
        OneDay => 1,
        TwoDays => 2,
        ThreeDays => 3,
        SevenDays => 7,
        FourteenDays => 14,
        _ => 0
    };

    public static bool IsWithinVisitWindow(DateOnly arrivalDate, string duration, DateOnly today)
    {
        var lastDay = arrivalDate.AddDays(GetStayDays(duration));
        return today >= arrivalDate && today <= lastDay;
    }
}
