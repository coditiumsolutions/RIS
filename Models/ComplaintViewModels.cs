using System.ComponentModel.DataAnnotations;

namespace ResidentPortal.Models;

public class ComplaintListItemViewModel
{
    public int Uid { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string DescriptionPreview { get; set; } = string.Empty;
    public string ResidentName { get; set; } = string.Empty;
    public string? ResidentUnit { get; set; }
    public string? WorkerName { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SlaDueAtUtc { get; set; }
    public bool IsOverdue { get; set; }
}

public class ComplaintListViewModel
{
    public IReadOnlyList<ComplaintListItemViewModel> Items { get; set; } = [];
    public string? StatusFilter { get; set; }
    public string? PriorityFilter { get; set; }
    public int? CategoryFilter { get; set; }
    public string? UnitFilter { get; set; }
    public string? ResidentFilter { get; set; }
    public string? WorkerFilter { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public IReadOnlyList<ComplaintCategory> Categories { get; set; } = [];
    public int OpenCount { get; set; }
    public int OverdueCount { get; set; }
    public int TodayCount { get; set; }
}

public class CreateComplaintViewModel
{
    [Required]
    [Display(Name = "Category")]
    public int? CategoryId { get; set; }

    [Required]
    [Display(Name = "Priority")]
    public string Priority { get; set; } = ComplaintPriority.Normal;

    [Required(ErrorMessage = "Please describe the issue.")]
    [StringLength(2000)]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    [StringLength(200)]
    [Display(Name = "Preferred visit time (optional)")]
    public string? PreferredVisitTime { get; set; }

    public IReadOnlyList<ComplaintCategory> Categories { get; set; } = [];
}

public class ComplaintHistoryItemViewModel
{
    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }
    public string? Note { get; set; }
    public string ActorUserType { get; set; } = string.Empty;
    public string? ActorDisplayName { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class ComplaintPhotoItemViewModel
{
    public int Uid { get; set; }
    public DateTime CapturedAtUtc { get; set; }
}

public class ComplaintDetailsViewModel
{
    public int Uid { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? PreferredVisitTime { get; set; }
    public string? DeclineReason { get; set; }
    public string? WorkerName { get; set; }
    public string? WorkerPhone { get; set; }
    public string ResidentName { get; set; } = string.Empty;
    public string? ResidentUnit { get; set; }
    public string? ResidentEmail { get; set; }
    public string? ResidentMobile { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SlaDueAtUtc { get; set; }
    public bool IsOverdue { get; set; }
    public bool CanCancel { get; set; }
    public IReadOnlyList<ComplaintHistoryItemViewModel> History { get; set; } = [];
    public IReadOnlyList<ComplaintPhotoItemViewModel> Photos { get; set; } = [];

    public DeclineComplaintViewModel DeclineForm { get; set; } = new();
    public AssignComplaintViewModel AssignForm { get; set; } = new();
    public NoteComplaintViewModel NoteForm { get; set; } = new();
}

public class DeclineComplaintViewModel
{
    public int ComplaintId { get; set; }

    [Required(ErrorMessage = "Decline reason is required.")]
    [StringLength(500)]
    [Display(Name = "Decline reason")]
    public string Reason { get; set; } = string.Empty;
}

public class AssignComplaintViewModel
{
    public int ComplaintId { get; set; }

    [Required(ErrorMessage = "Worker name is required.")]
    [StringLength(150)]
    [Display(Name = "Worker name")]
    public string WorkerName { get; set; } = string.Empty;

    [StringLength(30)]
    [Display(Name = "Worker phone (optional)")]
    public string? WorkerPhone { get; set; }

    [StringLength(1000)]
    [Display(Name = "Note (optional)")]
    public string? Note { get; set; }
}

public class NoteComplaintViewModel
{
    public int ComplaintId { get; set; }

    [Required]
    [StringLength(1000)]
    [Display(Name = "Note")]
    public string Note { get; set; } = string.Empty;
}

public class ComplaintCategoryEditViewModel
{
    public int? Uid { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public class AdminConfigurationViewModel
{
    [Display(Name = "SMTP host")]
    [StringLength(256)]
    public string? SmtpHost { get; set; }

    [Display(Name = "Port")]
    [Range(1, 65535)]
    public int SmtpPort { get; set; } = 587;

    [Display(Name = "Use SSL/TLS")]
    public bool SmtpUseSsl { get; set; } = true;

    [Display(Name = "Username")]
    [StringLength(256)]
    public string? SmtpUsername { get; set; }

    [Display(Name = "Password")]
    [DataType(DataType.Password)]
    [StringLength(200)]
    public string? SmtpPassword { get; set; }

    public bool HasSmtpPassword { get; set; }

    [Display(Name = "From email")]
    [EmailAddress]
    [StringLength(256)]
    public string? FromEmail { get; set; }

    [Display(Name = "From name")]
    [StringLength(150)]
    public string? FromName { get; set; }

    [Display(Name = "Enable email notifications")]
    public bool EmailNotificationsEnabled { get; set; }

    [Display(Name = "Urgent SLA (hours)")]
    [Range(1, 8760)]
    public int SlaUrgentHours { get; set; } = 24;

    [Display(Name = "Normal SLA (hours)")]
    [Range(1, 8760)]
    public int SlaNormalHours { get; set; } = 72;

    [Display(Name = "Low SLA (hours)")]
    [Range(1, 8760)]
    public int SlaLowHours { get; set; } = 168;

    public IReadOnlyList<ComplaintCategory> Categories { get; set; } = [];
}

public class EditResidentEmailViewModel
{
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    [StringLength(256)]
    [Display(Name = "Email")]
    public string? Email { get; set; }
}

public class ComplaintReportsViewModel
{
    public int OpenInitiated { get; set; }
    public int OpenApproved { get; set; }
    public int OpenAssigned { get; set; }
    public int OpenInProgress { get; set; }
    public int OpenResolved { get; set; }
    public int TodayCreated { get; set; }
    public int OverdueCount { get; set; }
    public double? AvgResolutionHours { get; set; }
    public IReadOnlyList<(string Worker, int Count)> WorkerWorkload { get; set; } = [];
    public IReadOnlyList<(string Category, int Count)> ByCategory { get; set; } = [];
    public IReadOnlyList<(string Priority, int Count)> ByPriority { get; set; } = [];
    public IReadOnlyList<(string Unit, int Count)> RepeatByUnit { get; set; } = [];
    public IReadOnlyList<(string Month, int Count)> MonthlyTrend { get; set; } = [];
    public bool IsAdvanced { get; set; }
}
