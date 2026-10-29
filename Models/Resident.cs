namespace ResidentPortal.Models;

public class Resident
{
    public int Uid { get; set; }

    public string ResidentName { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Mobile { get; set; }

    public string? Mobile2 { get; set; }

    public string? UnitNo { get; set; }

    public string? Street { get; set; }

    public string? Tower { get; set; }

    public string? Floor { get; set; }

    public string? Block { get; set; }

    public string Status { get; set; } = "Active";

    public bool IsActive { get; set; }

    public bool MustChangePassword { get; set; }

    public string? SecurityStamp { get; set; }

    public int FailedLoginAttempts { get; set; }

    public DateTime? LockoutEndUtc { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<VisitorRequest> VisitorRequests { get; set; } = new List<VisitorRequest>();
}
