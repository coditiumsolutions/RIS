namespace ResidentPortal.Security;

/// <summary>Permission constants and fixed Admin/Receptionist role maps. No role editor UI.</summary>
public static class StaffPermissions
{
    public const string ClaimType = "permission";
    public const string StaffKeyClaim = "staff_key";
    public const string StaffUserTypeClaim = "staff_user_type";

    public const string DeskDashboard = "Desk.Dashboard";
    public const string VisitorsView = "Visitors.View";
    public const string VisitorsCheckInOut = "Visitors.CheckInOut";
    public const string VisitorsWalkIn = "Visitors.WalkIn";
    public const string VisitorsApprove = "Visitors.Approve";
    public const string VisitorsReportsBasic = "Visitors.ReportsBasic";
    public const string ComplaintsView = "Complaints.View";
    public const string ComplaintsManage = "Complaints.Manage";
    public const string ComplaintsReportsBasic = "Complaints.ReportsBasic";
    public const string ComplaintsReportsAdvanced = "Complaints.ReportsAdvanced";
    public const string ResidentsManage = "Residents.Manage";
    public const string StaffManage = "Staff.Manage";
    public const string SettingsManage = "Settings.Manage";

    public static readonly IReadOnlyList<string> All =
    [
        DeskDashboard,
        VisitorsView,
        VisitorsCheckInOut,
        VisitorsWalkIn,
        VisitorsApprove,
        VisitorsReportsBasic,
        ComplaintsView,
        ComplaintsManage,
        ComplaintsReportsBasic,
        ComplaintsReportsAdvanced,
        ResidentsManage,
        StaffManage,
        SettingsManage
    ];

    public static readonly IReadOnlyList<string> ReceptionistPermissions =
    [
        DeskDashboard,
        VisitorsView,
        VisitorsCheckInOut,
        VisitorsWalkIn,
        VisitorsReportsBasic,
        ComplaintsView,
        ComplaintsManage,
        ComplaintsReportsBasic
    ];

    public static IReadOnlyList<string> ForRole(string role) =>
        string.Equals(role, PortalRoles.Admin, StringComparison.OrdinalIgnoreCase)
            ? All
            : string.Equals(role, PortalRoles.Receptionist, StringComparison.OrdinalIgnoreCase)
                ? ReceptionistPermissions
                : Array.Empty<string>();

    public static string PolicyName(string permission) => $"Perm:{permission}";

    public static string StaffKey(string userType, int userId) =>
        $"{userType.ToLowerInvariant()}:{userId}";
}
