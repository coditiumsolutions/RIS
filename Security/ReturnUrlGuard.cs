namespace ResidentPortal.Security;

public static class ReturnUrlGuard
{
    public static bool IsSafe(string? returnUrl, bool adminPortal)
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
        return adminPortal ? isAdminPath : !isAdminPath;
    }
}
