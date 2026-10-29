namespace ResidentPortal.Security;

public static class AuthSchemes
{
    public const string Resident = "Resident";

    public const string Admin = "Admin";

    public const string Receptionist = "Receptionist";
}

public static class AuthPolicies
{
    public const string ResidentOnly = "ResidentOnly";

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
