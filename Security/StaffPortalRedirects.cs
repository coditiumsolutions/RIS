namespace ResidentPortal.Security;

/// <summary>Maps legacy /AdminPortal and /ReceptionPortal paths to /Staff equivalents.</summary>
public static class StaffPortalRedirects
{
    public static string? MapLegacyPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var trimmed = path.TrimEnd('/');
        if (trimmed.Length == 0)
            trimmed = "/";

        if (trimmed.StartsWith("/AdminPortal", StringComparison.OrdinalIgnoreCase))
            return MapAdmin(trimmed);

        if (trimmed.StartsWith("/ReceptionPortal", StringComparison.OrdinalIgnoreCase))
            return MapReception(trimmed);

        return null;
    }

    private static string MapAdmin(string path)
    {
        var rest = path.Length > "/AdminPortal".Length
            ? path["/AdminPortal".Length..]
            : string.Empty;

        if (string.IsNullOrEmpty(rest) || rest.Equals("/", StringComparison.Ordinal))
            return "/Staff";

        if (rest.StartsWith("/Account", StringComparison.OrdinalIgnoreCase))
            return "/Staff";

        if (rest.StartsWith("/Dashboard", StringComparison.OrdinalIgnoreCase))
            return "/Staff/Dashboard" + rest["/Dashboard".Length..];

        if (rest.StartsWith("/Visitors", StringComparison.OrdinalIgnoreCase))
            return "/Staff/Visitors" + rest["/Visitors".Length..];

        if (rest.StartsWith("/Complaints", StringComparison.OrdinalIgnoreCase))
            return "/Staff/Complaints" + rest["/Complaints".Length..];

        if (rest.StartsWith("/Configurations", StringComparison.OrdinalIgnoreCase))
            return "/Staff/Configurations" + rest["/Configurations".Length..];

        if (rest.StartsWith("/Residents", StringComparison.OrdinalIgnoreCase))
            return "/Staff/Residents" + rest["/Residents".Length..];

        if (rest.StartsWith("/Users", StringComparison.OrdinalIgnoreCase))
            return "/Staff/Users" + rest["/Users".Length..];

        if (rest.StartsWith("/Password", StringComparison.OrdinalIgnoreCase))
            return "/Staff/Password" + rest["/Password".Length..];

        return "/Staff";
    }

    private static string MapReception(string path)
    {
        var rest = path.Length > "/ReceptionPortal".Length
            ? path["/ReceptionPortal".Length..]
            : string.Empty;

        if (string.IsNullOrEmpty(rest) || rest.Equals("/", StringComparison.Ordinal))
            return "/Staff";

        if (rest.StartsWith("/Account", StringComparison.OrdinalIgnoreCase))
            return "/Staff";

        // Reception desk dashboard becomes /Staff/Desk
        if (rest.StartsWith("/Dashboard", StringComparison.OrdinalIgnoreCase))
            return "/Staff/Desk" + rest["/Dashboard".Length..];

        if (rest.StartsWith("/Visitors", StringComparison.OrdinalIgnoreCase))
            return "/Staff/Visitors" + rest["/Visitors".Length..];

        if (rest.StartsWith("/Complaints", StringComparison.OrdinalIgnoreCase))
            return "/Staff/Complaints" + rest["/Complaints".Length..];

        if (rest.StartsWith("/Password", StringComparison.OrdinalIgnoreCase))
            return "/Staff/Password" + rest["/Password".Length..];

        return "/Staff";
    }
}

public sealed class LegacyStaffPortalRedirectMiddleware
{
    private readonly RequestDelegate _next;

    public LegacyStaffPortalRedirectMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var mapped = StaffPortalRedirects.MapLegacyPath(path);
        if (mapped is null)
            return _next(context);

        var target = mapped + context.Request.QueryString.Value;
        context.Response.Redirect(target, permanent: false);
        return Task.CompletedTask;
    }
}

public static class LegacyStaffPortalRedirectMiddlewareExtensions
{
    public static IApplicationBuilder UseLegacyStaffPortalRedirects(this IApplicationBuilder app) =>
        app.UseMiddleware<LegacyStaffPortalRedirectMiddleware>();
}
