namespace ResidentPortal.Models;

public class AuditLog
{
    public int Uid { get; set; }

    public string UserType { get; set; } = string.Empty;

    public int? UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
