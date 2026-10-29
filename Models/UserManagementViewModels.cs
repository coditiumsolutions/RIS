using System.ComponentModel.DataAnnotations;

namespace ResidentPortal.Models;

public class UserListViewModel
{
    public IReadOnlyList<AdminUserListItem> Admins { get; set; } = [];
    public IReadOnlyList<ReceptionistListItem> Receptionists { get; set; } = [];
    public int TotalAdminCount { get; set; }
    public int TotalReceptionistCount { get; set; }
}

public class AdminUserListItem
{
    public int Uid { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ReceptionistListItem
{
    public int Uid { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateReceptionistViewModel
{
    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(150, ErrorMessage = "Name cannot exceed 150 characters.")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

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
}

public class EditReceptionistViewModel
{
    public int Uid { get; set; }

    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(150, ErrorMessage = "Name cannot exceed 150 characters.")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Username is required.")]
    [StringLength(100, ErrorMessage = "Username cannot exceed 100 characters.")]
    [Display(Name = "Username")]
    public string Username { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Invalid email address.")]
    [StringLength(256, ErrorMessage = "Email cannot exceed 256 characters.")]
    [Display(Name = "Email (Optional)")]
    public string? Email { get; set; }

    [Display(Name = "Account Active")]
    public bool IsActive { get; set; }
}

public class ResetUserPasswordViewModel
{
    public int Uid { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty;

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

public class ReceptionistDashboardViewModel
{
    public string ReceptionistName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public int ExpectedVisitorsToday { get; set; }
    public int CheckedInToday { get; set; }
    public int CheckedOutToday { get; set; }
    public int WalkInsToday { get; set; }
    public string TodayIso { get; set; } = string.Empty;
    public IReadOnlyList<VisitorRequestListItem> RecentActivity { get; set; } = [];
}
