namespace ResidentPortal.Models;

public class Resident
{
    public int Uid { get; set; }

    public string ResidentName { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Mobile { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}
