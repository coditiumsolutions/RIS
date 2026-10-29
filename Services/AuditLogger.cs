using ResidentPortal.Data;
using ResidentPortal.Models;

namespace ResidentPortal.Services;

public sealed class AuditLogger
{
    private readonly ApplicationDbContext _db;

    public AuditLogger(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task WriteAsync(string userType, int? userId, string action, CancellationToken cancellationToken)
    {
        var safeAction = action.Length <= 300 ? action : action[..300];
        _db.AuditLogs.Add(new AuditLog
        {
            UserType = userType,
            UserId = userId,
            Action = safeAction,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
    }
}
