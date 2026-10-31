namespace ResidentPortal.Security;

public static class AuthSchemes
{
    public const string Resident = "Resident";

    /// <summary>Unified Staff portal cookie (Admin + Receptionist).</summary>
    public const string Staff = "Staff";

    // Legacy scheme names kept so old cookies expire cleanly; no longer registered as active cookies.
    public const string Admin = "Admin";

    public const string Receptionist = "Receptionist";
}

public static class AuthPolicies
{
    public const string ResidentOnly = "ResidentOnly";

    public const string StaffOnly = "StaffOnly";

    // Backward-compatible aliases used during migration / redirects.
    public const string AdminOnly = "AdminOnly";

    public const string ReceptionistOnly = "ReceptionistOnly";

    public const string AdminOrReceptionist = "AdminOrReceptionist";
}

public static class PortalRoles
{
    public const string Resident = "Resident";

    public const string Admin = "Admin";

    public const string Receptionist = "Receptionist";
}
