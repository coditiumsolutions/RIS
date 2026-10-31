namespace ResidentPortal.Models;

public class ComplaintCategory
{
    public int Uid { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public ICollection<Complaint> Complaints { get; set; } = new List<Complaint>();
}

public class Complaint
{
    public int Uid { get; set; }
    public int ResidentId { get; set; }
    public int CategoryId { get; set; }
    public string Priority { get; set; } = ComplaintPriority.Normal;
    public string Description { get; set; } = string.Empty;
    public string? PreferredVisitTime { get; set; }
    public string Status { get; set; } = ComplaintStatus.Initiated;
    public string? DeclineReason { get; set; }
    public string? WorkerName { get; set; }
    public string? WorkerPhone { get; set; }
    public DateTime? AssignedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public DateTime? SlaDueAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public Resident Resident { get; set; } = null!;
    public ComplaintCategory Category { get; set; } = null!;
    public ICollection<ComplaintHistory> History { get; set; } = new List<ComplaintHistory>();
    public ICollection<ComplaintPhoto> Photos { get; set; } = new List<ComplaintPhoto>();
}

public class ComplaintHistory
{
    public int Uid { get; set; }
    public int ComplaintId { get; set; }
    public string ActorUserType { get; set; } = string.Empty;
    public int? ActorUserId { get; set; }
    public string? ActorDisplayName { get; set; }
    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Complaint Complaint { get; set; } = null!;
}

public class ComplaintPhoto
{
    public int Uid { get; set; }
    public int ComplaintId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public int SizeBytes { get; set; }
    public string UploadedByUserType { get; set; } = string.Empty;
    public int? UploadedByUserId { get; set; }
    public DateTime CapturedAtUtc { get; set; }
    public Complaint Complaint { get; set; } = null!;
}

public class SmtpSettings
{
    public int Uid { get; set; }
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? PasswordProtected { get; set; }
    public string? FromEmail { get; set; }
    public string? FromName { get; set; }
    public bool EmailNotificationsEnabled { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public static class ComplaintStatus
{
    public const string Initiated = "Initiated";
    public const string Declined = "Declined";
    public const string Approved = "Approved";
    public const string Assigned = "Assigned";
    public const string WorkInProgress = "WorkInProgress";
    public const string Resolved = "Resolved";
    public const string Closed = "Closed";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlyList<string> All =
    [
        Initiated, Declined, Approved, Assigned, WorkInProgress, Resolved, Closed, Cancelled
    ];

    public static string Display(string status) => status switch
    {
        WorkInProgress => "Work in progress",
        _ => status
    };
}

public static class ComplaintPriority
{
    public const string Urgent = "Urgent";
    public const string Normal = "Normal";
    public const string Low = "Low";

    public static readonly IReadOnlyList<string> All = [Urgent, Normal, Low];
}
