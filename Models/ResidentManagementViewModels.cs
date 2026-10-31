using System.ComponentModel.DataAnnotations;

namespace ResidentPortal.Models;

public class ResidentListViewModel
{
    public IReadOnlyList<ResidentListItem> Residents { get; set; } = [];
    public int TotalCount { get; set; }
    public int ActiveCount { get; set; }
    public string? SearchQuery { get; set; }
}

public class ResidentListItem
{
    public int Uid { get; set; }
    public string ResidentName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Mobile { get; set; }
    public string? UnitNo { get; set; }
    public string? Tower { get; set; }
    public string? Block { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateResidentViewModel
{
    [Required(ErrorMessage = "Resident name is required.")]
    [StringLength(150, ErrorMessage = "Name cannot exceed 150 characters.")]
    [Display(Name = "Full Name")]
    public string ResidentName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Customer ID is required.")]
    [StringLength(50, ErrorMessage = "Customer ID cannot exceed 50 characters.")]
    [RegularExpression(@"^[A-Za-z0-9_-]+$", ErrorMessage = "Customer ID can only contain letters, numbers, hyphens, and underscores.")]
    [Display(Name = "Customer ID")]
    public string CustomerId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Username is required.")]
    [StringLength(100, ErrorMessage = "Username cannot exceed 100 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9._-]+$", ErrorMessage = "Username can only contain letters, numbers, dots, underscores, and hyphens.")]
    [Display(Name = "Username")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(200, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "Initial Password")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm password is required.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Invalid email address.")]
    [StringLength(256, ErrorMessage = "Email cannot exceed 256 characters.")]
    [Display(Name = "Email (Optional)")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Mobile number is required.")]
    [StringLength(30, ErrorMessage = "Mobile cannot exceed 30 characters.")]
    [PakistaniMobile]
    [Display(Name = "Mobile")]
    public string? Mobile { get; set; }

    [StringLength(30, ErrorMessage = "Mobile 2 cannot exceed 30 characters.")]
    [PakistaniMobile]
    [Display(Name = "Mobile 2 (Optional)")]
    public string? Mobile2 { get; set; }

    [StringLength(50, ErrorMessage = "Unit number cannot exceed 50 characters.")]
    [Display(Name = "Unit No")]
    public string? UnitNo { get; set; }

    [StringLength(150, ErrorMessage = "Street cannot exceed 150 characters.")]
    [Display(Name = "Street")]
    public string? Street { get; set; }

    [StringLength(50, ErrorMessage = "Tower cannot exceed 50 characters.")]
    [Display(Name = "Tower")]
    public string? Tower { get; set; }

    [StringLength(20, ErrorMessage = "Floor cannot exceed 20 characters.")]
    [Display(Name = "Floor")]
    public string? Floor { get; set; }

    [StringLength(50, ErrorMessage = "Block cannot exceed 50 characters.")]
    [Display(Name = "Block")]
    public string? Block { get; set; }

    [Display(Name = "Status")]
    public string Status { get; set; } = "Active";
}

public class EditResidentViewModel
{
    public int Uid { get; set; }

    [Required(ErrorMessage = "Resident name is required.")]
    [StringLength(150, ErrorMessage = "Name cannot exceed 150 characters.")]
    [Display(Name = "Full Name")]
    public string ResidentName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Customer ID is required.")]
    [StringLength(50, ErrorMessage = "Customer ID cannot exceed 50 characters.")]
    [RegularExpression(@"^[A-Za-z0-9_-]+$", ErrorMessage = "Customer ID can only contain letters, numbers, hyphens, and underscores.")]
    [Display(Name = "Customer ID")]
    public string CustomerId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Username is required.")]
    [StringLength(100, ErrorMessage = "Username cannot exceed 100 characters.")]
    [Display(Name = "Username")]
    public string Username { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Invalid email address.")]
    [StringLength(256, ErrorMessage = "Email cannot exceed 256 characters.")]
    [Display(Name = "Email (Optional)")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Mobile number is required.")]
    [StringLength(30, ErrorMessage = "Mobile cannot exceed 30 characters.")]
    [PakistaniMobile]
    [Display(Name = "Mobile")]
    public string? Mobile { get; set; }

    [StringLength(30, ErrorMessage = "Mobile 2 cannot exceed 30 characters.")]
    [PakistaniMobile]
    [Display(Name = "Mobile 2 (Optional)")]
    public string? Mobile2 { get; set; }

    [StringLength(50, ErrorMessage = "Unit number cannot exceed 50 characters.")]
    [Display(Name = "Unit No")]
    public string? UnitNo { get; set; }

    [StringLength(150, ErrorMessage = "Street cannot exceed 150 characters.")]
    [Display(Name = "Street")]
    public string? Street { get; set; }

    [StringLength(50, ErrorMessage = "Tower cannot exceed 50 characters.")]
    [Display(Name = "Tower")]
    public string? Tower { get; set; }

    [StringLength(20, ErrorMessage = "Floor cannot exceed 20 characters.")]
    [Display(Name = "Floor")]
    public string? Floor { get; set; }

    [StringLength(50, ErrorMessage = "Block cannot exceed 50 characters.")]
    [Display(Name = "Block")]
    public string? Block { get; set; }

    [Display(Name = "Status")]
    public string Status { get; set; } = "Active";

    [Display(Name = "Account Active")]
    public bool IsActive { get; set; }
}

public class ResetResidentPasswordViewModel
{
    public int Uid { get; set; }
    public string ResidentName { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required.")]
    [StringLength(200, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm password is required.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Display(Name = "Require password change on next login")]
    public bool MustChangePassword { get; set; } = true;
}

public static class ResidentStatuses
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";
    public const string Suspended = "Suspended";

    public static readonly IReadOnlyList<string> All = new[] { Active, Inactive, Suspended };
}
