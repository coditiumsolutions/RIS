namespace ResidentPortal.Models;

public class ReceptionDeskSummaryDto
{
    public string Today { get; set; } = string.Empty;
    public int ExpectedToday { get; set; }
    public int CheckedInNow { get; set; }
    public int CheckedOutToday { get; set; }
    public int WalkInsToday { get; set; }
}

public class ReceptionDeskVisitorDto
{
    public int Id { get; set; }
    public string VisitorName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Cnic { get; set; }
    public string? Vehicle { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public int Guests { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public string ArrivalDate { get; set; } = string.Empty;
    public string ResidentName { get; set; } = string.Empty;
    public string? ResidentUnit { get; set; }
    public bool IsWalkIn { get; set; }
    public bool CanCheckIn { get; set; }
    public bool CanCheckOut { get; set; }
    public string? CheckedInAt { get; set; }
    public string? CheckedOutAt { get; set; }
}

public class ReceptionDeskDayDto
{
    public string Date { get; set; } = string.Empty;
    public string Filter { get; set; } = "all";
    public IReadOnlyList<ReceptionDeskVisitorDto> Visitors { get; set; } = [];
}

public class ReceptionDeskSearchDto
{
    public string? Query { get; set; }
    public string? Date { get; set; }
    public string Status { get; set; } = "all";
    public int Offset { get; set; }
    public int Limit { get; set; }
    public int Total { get; set; }
    public bool HasMore { get; set; }
    public IReadOnlyList<ReceptionDeskVisitorDto> Visitors { get; set; } = [];
}

public class ReceptionDeskCalendarDayDto
{
    public string Date { get; set; } = string.Empty;
    public int Expected { get; set; }
    public int CheckedIn { get; set; }
    public int CheckedOut { get; set; }
    public int Total { get; set; }
}

public class ReceptionDeskCalendarDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public IReadOnlyList<ReceptionDeskCalendarDayDto> Days { get; set; } = [];
}
