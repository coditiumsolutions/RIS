using System.Security.Claims;

namespace ResidentPortal.Security;

public static class ClaimsPrincipalExtensions
{
    public static int? GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id) ? id : null;
    }

    public static string? GetStaffKey(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(StaffPermissions.StaffKeyClaim);

    public static string? GetStaffUserType(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(StaffPermissions.StaffUserTypeClaim);

    public static bool HasPermission(this ClaimsPrincipal principal, string permission) =>
        principal.HasClaim(StaffPermissions.ClaimType, permission);
}
