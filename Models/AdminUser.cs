namespace ResidentPortal.Models;

public class AdminUser
{
    public int Uid { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string? Email { get; set; }

    public bool IsActive { get; set; }

    public bool MustChangePassword { get; set; }

    public string? SecurityStamp { get; set; }

    public int FailedLoginAttempts { get; set; }

    public DateTime? LockoutEndUtc { get; set; }

    public DateTime CreatedAt { get; set; }
}
