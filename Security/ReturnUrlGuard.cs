namespace ResidentPortal.Security;

public enum PortalType
{
    Resident,
    Admin,
    Receptionist
}

public static class ReturnUrlGuard
{
    public static bool IsSafe(string? returnUrl, bool adminPortal) =>
        IsSafe(returnUrl, adminPortal ? PortalType.Admin : PortalType.Resident);

    public static bool IsSafe(string? returnUrl, PortalType portalType)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return false;

        if (!returnUrl.StartsWith('/'))
            return false;

        if (returnUrl.StartsWith("//", StringComparison.Ordinal) || returnUrl.StartsWith("/\\", StringComparison.Ordinal))
            return false;

        if (returnUrl.Contains('\\') || returnUrl.Contains('\r') || returnUrl.Contains('\n'))
            return false;

        var isAdminPath = returnUrl.StartsWith("/adminportal", StringComparison.OrdinalIgnoreCase);
        var isReceptionPath = returnUrl.StartsWith("/receptionportal", StringComparison.OrdinalIgnoreCase);

        return portalType switch
        {
            PortalType.Admin => isAdminPath,
            PortalType.Receptionist => isReceptionPath,
            PortalType.Resident => !isAdminPath && !isReceptionPath,
            _ => false
        };
    }
}
