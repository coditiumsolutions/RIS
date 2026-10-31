using System.Security.Claims;
using ResidentPortal.Models;

namespace ResidentPortal.Security;

/// <summary>Builds the unified Staff cookie principal (Admin or Receptionist) with permission claims.</summary>
public static class StaffPrincipalFactory
{
    public static ClaimsPrincipal Create(AdminUser admin) =>
        CreatePrincipal(
            userId: admin.Uid,
            fullName: admin.FullName,
            role: PortalRoles.Admin,
            username: admin.Username,
            email: admin.Email,
            securityStamp: admin.SecurityStamp,
            mustChangePassword: admin.MustChangePassword,
            staffKeyPrefix: "admin");

    public static ClaimsPrincipal Create(Receptionist receptionist) =>
        CreatePrincipal(
            userId: receptionist.Uid,
            fullName: receptionist.FullName,
            role: PortalRoles.Receptionist,
            username: receptionist.Username,
            email: receptionist.Email,
            securityStamp: receptionist.SecurityStamp,
            mustChangePassword: receptionist.MustChangePassword,
            staffKeyPrefix: "reception");

    public static ClaimsPrincipal Create(
        int userId,
        string fullName,
        string role,
        string username,
        string? email,
        string? securityStamp,
        bool mustChangePassword)
    {
        var prefix = string.Equals(role, PortalRoles.Admin, StringComparison.OrdinalIgnoreCase)
            ? "admin"
            : "reception";

        return CreatePrincipal(
            userId,
            fullName,
            role,
            username,
            email,
            securityStamp,
            mustChangePassword,
            prefix);
    }

    private static ClaimsPrincipal CreatePrincipal(
        int userId,
        string fullName,
        string role,
        string username,
        string? email,
        string? securityStamp,
        bool mustChangePassword,
        string staffKeyPrefix)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, fullName),
            new(ClaimTypes.Role, role),
            new("username", username),
            new("SecurityStamp", securityStamp ?? string.Empty),
            new(StaffPermissions.StaffKeyClaim, StaffPermissions.StaffKey(staffKeyPrefix, userId)),
            new(StaffPermissions.StaffUserTypeClaim, role)
        };

        if (mustChangePassword)
            claims.Add(new Claim("MustChangePassword", bool.TrueString));

        if (!string.IsNullOrWhiteSpace(email))
            claims.Add(new Claim(ClaimTypes.Email, email));

        foreach (var permission in StaffPermissions.ForRole(role))
            claims.Add(new Claim(StaffPermissions.ClaimType, permission));

        var identity = new ClaimsIdentity(claims, AuthSchemes.Staff);
        return new ClaimsPrincipal(identity);
    }
}
