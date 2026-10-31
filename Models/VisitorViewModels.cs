using System.ComponentModel.DataAnnotations;

namespace ResidentPortal.Models;

public class RegisterVisitorViewModel
{
    [Required(ErrorMessage = "Visitor name is required.")]
    [StringLength(100, ErrorMessage = "Visitor name cannot exceed 100 characters.")]
    [Display(Name = "Visitor Name")]
    public string VisitorName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required.")]
    [StringLength(30, ErrorMessage = "Phone number cannot exceed 30 characters.")]
    [ContactPhone]
    [Display(Name = "Phone")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Purpose of visit is required.")]
    [StringLength(500, ErrorMessage = "Purpose cannot exceed 500 characters.")]
    [Display(Name = "Purpose of Visit")]
    public string Purpose { get; set; } = string.Empty;

    [Required(ErrorMessage = "Duration is required.")]
    [StringLength(30, ErrorMessage = "Duration cannot exceed 30 characters.")]
    [AllowedDuration]
    [Display(Name = "Duration")]
    public string Duration { get; set; } = string.Empty;

    [Required(ErrorMessage = "Arrival date is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "Arrival Date")]
    public DateOnly? ArrivalDate { get; set; }

    [Required(ErrorMessage = "Number of guests is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Number of guests must be at least 1.")]
    [Display(Name = "Number of Guests")]
    public int? NumberOfGuests { get; set; }

    [StringLength(30, ErrorMessage = "CNIC cannot exceed 30 characters.")]
    [Cnic]
    [Display(Name = "CNIC (Optional)")]
    public string? CNIC { get; set; }

    [StringLength(30, ErrorMessage = "Vehicle registration cannot exceed 30 characters.")]
    [Display(Name = "Vehicle Registration No (Optional)")]
    public string? VehicleRegistrationNo { get; set; }
}

public class VisitorRequestListItem
{
    public int Uid { get; set; }
    public string VisitorName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public DateOnly ArrivalDate { get; set; }
    public int NumberOfGuests { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsWalkIn { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public DateTime? CheckedOutAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public string ResidentName { get; set; } = string.Empty;
    public string? ResidentUnit { get; set; }
}

public class ResidentVisitorListViewModel
{
    public IReadOnlyList<VisitorRequestListItem> Visitors { get; set; } = [];
    public int TotalCount { get; set; }
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int CheckedInCount { get; set; }
}

public class AdminVisitorListViewModel
{
    public IReadOnlyList<VisitorRequestListItem> Visitors { get; set; } = [];
    public int TotalCount { get; set; }
    public int PendingCount { get; set; }
    public int ApprovedTodayCount { get; set; }
    public int CheckedInCount { get; set; }
    public bool ApprovalRequired { get; set; }
    public string? StatusFilter { get; set; }
    public string? SearchQuery { get; set; }
}

public class ReceptionistVisitorListViewModel
{
    public IReadOnlyList<VisitorRequestListItem> Visitors { get; set; } = [];
    public int ExpectedTodayCount { get; set; }
    public int CheckedInCount { get; set; }
    public int CheckedOutTodayCount { get; set; }
    public string? SearchQuery { get; set; }
    public DateOnly? DateFilter { get; set; }
}

public class WalkInVisitorViewModel
{
    [Required(ErrorMessage = "Visitor name is required.")]
    [StringLength(100, ErrorMessage = "Visitor name cannot exceed 100 characters.")]
    [Display(Name = "Visitor Name")]
    public string VisitorName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required.")]
    [StringLength(30, ErrorMessage = "Phone number cannot exceed 30 characters.")]
    [ContactPhone]
    [Display(Name = "Phone")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select the visiting resident.")]
    [Display(Name = "Visiting Resident")]
    public int? ResidentId { get; set; }

    [Required(ErrorMessage = "Purpose of visit is required.")]
    [StringLength(500, ErrorMessage = "Purpose cannot exceed 500 characters.")]
    [Display(Name = "Purpose of Visit")]
    public string Purpose { get; set; } = string.Empty;

    [Required(ErrorMessage = "Duration is required.")]
    [StringLength(30, ErrorMessage = "Duration cannot exceed 30 characters.")]
    [AllowedDuration]
    [Display(Name = "Duration")]
    public string Duration { get; set; } = string.Empty;

    [Required(ErrorMessage = "Number of guests is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Number of guests must be at least 1.")]
    [Display(Name = "Number of Guests")]
    public int? NumberOfGuests { get; set; }

    [StringLength(30, ErrorMessage = "CNIC cannot exceed 30 characters.")]
    [Cnic]
    [Display(Name = "CNIC (Optional)")]
    public string? CNIC { get; set; }

    [StringLength(30, ErrorMessage = "Vehicle registration cannot exceed 30 characters.")]
    [Display(Name = "Vehicle Registration No (Optional)")]
    public string? VehicleRegistrationNo { get; set; }

    public IReadOnlyList<ResidentSelectItem> AvailableResidents { get; set; } = [];
}

public class ResidentSelectItem
{
    public int Uid { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? UnitNo { get; set; }
    public string DisplayName => string.IsNullOrWhiteSpace(UnitNo) ? Name : $"{Name} (Unit {UnitNo})";
}

public class VisitorDetailsViewModel
{
    public int Uid { get; set; }
    public string VisitorName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public DateOnly ArrivalDate { get; set; }
    public int NumberOfGuests { get; set; }
    public string? CNIC { get; set; }
    public string? VehicleRegistrationNo { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsWalkIn { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public DateTime? CheckedOutAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string ResidentName { get; set; } = string.Empty;
    public string? ResidentUnit { get; set; }
    public string? ResidentMobile { get; set; }
    public string? DeclineReason { get; set; }
    public bool CanCheckIn { get; set; }
    public IReadOnlyList<VisitorPhotoItemViewModel> Photos { get; set; } = [];
}

public class VisitorPhotoItemViewModel
{
    public int Uid { get; set; }
    public string Kind { get; set; } = string.Empty;
    public DateTime CapturedAtUtc { get; set; }
}

public class DeclineVisitorViewModel
{
    public int Uid { get; set; }
    public string VisitorName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please provide a reason for declining.")]
    [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters.")]
    [Display(Name = "Reason for Declining")]
    public string Reason { get; set; } = string.Empty;
}
