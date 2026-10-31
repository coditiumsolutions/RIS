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
            var redirectUrl = area switch
            {
                "Staff" => "/Staff/Password?mustChange=true",
                "AdminPortal" => "/AdminPortal/Password?mustChange=true",
                "ReceptionPortal" => "/ReceptionPortal/Password?mustChange=true",
                _ => "/Password?mustChange=true"
            };

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
        else if (user.IsInRole(PortalRoles.Admin) || user.IsInRole(PortalRoles.Receptionist))
        {
            var area = context.RouteData.Values["area"]?.ToString();
            var staffArea = string.Equals(area, "Staff", StringComparison.OrdinalIgnoreCase)
                || string.Equals(user.GetStaffUserType(), PortalRoles.Admin, StringComparison.OrdinalIgnoreCase)
                || string.Equals(user.GetStaffUserType(), PortalRoles.Receptionist, StringComparison.OrdinalIgnoreCase);

            // Prefer Staff portal when the Staff cookie / area is in use.
            if (staffArea && !string.IsNullOrEmpty(user.GetStaffUserType()))
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
            else if (user.IsInRole(PortalRoles.Admin))
            {
                var db = context.HttpContext.RequestServices.GetRequiredService<ResidentPortal.Data.ApplicationDbContext>();
                var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdStr, out var userId))
                {
                    var admin = await db.AdminUsers.FindAsync(userId);
                    if (admin?.MustChangePassword == true)
                    {
                        context.Result = new RedirectResult("/AdminPortal/Password?mustChange=true");
                        return;
                    }
                }
            }
            else if (user.IsInRole(PortalRoles.Receptionist))
            {
                var db = context.HttpContext.RequestServices.GetRequiredService<ResidentPortal.Data.ApplicationDbContext>();
                var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdStr, out var userId))
                {
                    var receptionist = await db.Receptionists.FindAsync(userId);
                    if (receptionist?.MustChangePassword == true)
                    {
                        context.Result = new RedirectResult("/ReceptionPortal/Password?mustChange=true");
                        return;
                    }
                }
            }
        }

        await next();
    }
}
