using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ResidentPortal.Security;

public class MustChangePasswordFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> AllowedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password/Index",
        "Account/Logout",
        "Account/Forbidden",
        "Home/Logout",
        "Home/Error",
        "Home/NotFound"
    };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        if (!user.Identity?.IsAuthenticated ?? true)
        {
            await next();
            return;
        }

        var controllerName = context.RouteData.Values["controller"]?.ToString() ?? string.Empty;
        var actionName = context.RouteData.Values["action"]?.ToString() ?? string.Empty;
        var currentPath = $"{controllerName}/{actionName}";

        if (AllowedPaths.Contains(currentPath))
        {
            await next();
            return;
        }

        var mustChange = user.FindFirstValue("MustChangePassword");
        if (string.Equals(mustChange, "True", StringComparison.OrdinalIgnoreCase))
        {
            var area = context.RouteData.Values["area"]?.ToString();
            var redirectUrl = string.Equals(area, "Staff", StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrEmpty(user.GetStaffUserType())
                ? "/Staff/Password?mustChange=true"
                : "/Password?mustChange=true";

            context.Result = new RedirectResult(redirectUrl);
            return;
        }

        if (user.IsInRole(PortalRoles.Resident))
        {
            var db = context.HttpContext.RequestServices.GetRequiredService<ResidentPortal.Data.ApplicationDbContext>();
            var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdStr, out var userId))
            {
                var resident = await db.Residents.FindAsync(userId);
                if (resident?.MustChangePassword == true)
                {
                    context.Result = new RedirectResult("/Password?mustChange=true");
                    return;
                }
            }
        }
        else if (!string.IsNullOrEmpty(user.GetStaffUserType()))
        {
            var db = context.HttpContext.RequestServices.GetRequiredService<ResidentPortal.Data.ApplicationDbContext>();
            var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdStr, out var userId))
            {
                var userType = user.GetStaffUserType();
                var mustChangeDb = false;
                if (string.Equals(userType, PortalRoles.Admin, StringComparison.OrdinalIgnoreCase))
                {
                    var admin = await db.AdminUsers.FindAsync(userId);
                    mustChangeDb = admin?.MustChangePassword == true;
                }
                else if (string.Equals(userType, PortalRoles.Receptionist, StringComparison.OrdinalIgnoreCase))
                {
                    var receptionist = await db.Receptionists.FindAsync(userId);
                    mustChangeDb = receptionist?.MustChangePassword == true;
                }

                if (mustChangeDb)
                {
                    context.Result = new RedirectResult("/Staff/Password?mustChange=true");
                    return;
                }
            }
        }

        await next();
    }
}
