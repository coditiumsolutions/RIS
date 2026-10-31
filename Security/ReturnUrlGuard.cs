namespace ResidentPortal.Security;

public enum PortalType
{
    Resident,
    Admin,
    Receptionist,
    Staff
}

public static class ReturnUrlGuard
{
    public static bool IsSafe(string? returnUrl, bool adminPortal) =>
        IsSafe(returnUrl, adminPortal ? PortalType.Admin : PortalType.Resident);

    public static bool IsSafe(string? returnUrl, PortalType portalType)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return false;

        if (ContainsControlOrWhitespaceChars(returnUrl))
            return false;

        if (!IsLocalUrl(returnUrl))
            return false;

        var isAdminPath = returnUrl.StartsWith("/adminportal", StringComparison.OrdinalIgnoreCase);
        var isReceptionPath = returnUrl.StartsWith("/receptionportal", StringComparison.OrdinalIgnoreCase);
        var isStaffPath = returnUrl.StartsWith("/staff", StringComparison.OrdinalIgnoreCase);

        return portalType switch
        {
            PortalType.Admin => isAdminPath,
            PortalType.Receptionist => isReceptionPath,
            PortalType.Staff => isStaffPath || isAdminPath || isReceptionPath,
            PortalType.Resident => !isAdminPath && !isReceptionPath && !isStaffPath,
            _ => false
        };
    }

    // Mirrors Microsoft.AspNetCore.Mvc.Routing.UrlHelper.IsLocalUrl
    public static bool IsLocalUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return false;

        if (url.Contains('\\'))
            return false;

        if (url[0] == '/' && (url.Length == 1 || (url[1] != '/' && url[1] != '\\')))
            return true;

        if (url.Length > 1 && url[0] == '~' && url[1] == '/')
            return true;

        return false;
    }

    private static bool ContainsControlOrWhitespaceChars(string value)
    {
        foreach (var c in value)
        {
            if (char.IsControl(c) || char.IsWhiteSpace(c))
                return true;
        }

        return false;
    }
}
