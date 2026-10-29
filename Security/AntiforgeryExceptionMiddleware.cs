using Microsoft.AspNetCore.Antiforgery;

namespace ResidentPortal.Security;

public class AntiforgeryExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AntiforgeryExceptionMiddleware> _logger;

    private static readonly HashSet<string> LoginPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/login",
        "/adminportal",
        "/adminportal/account",
        "/adminportal/account/index",
        "/receptionportal",
        "/receptionportal/account",
        "/receptionportal/account/index"
    };

    public AntiforgeryExceptionMiddleware(RequestDelegate next, ILogger<AntiforgeryExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AntiforgeryValidationException)
        {
            var path = context.Request.Path.Value ?? string.Empty;

            if (HttpMethods.IsPost(context.Request.Method) && IsLoginPath(path))
            {
                _logger.LogWarning("Antiforgery validation failed on login form at {Path}. Redirecting with session expired message.", path);
                var redirectUrl = GetLoginRedirectUrl(path);
                context.Response.Redirect(redirectUrl);
                return;
            }

            throw;
        }
    }

    private static bool IsLoginPath(string path)
    {
        var trimmed = path.TrimEnd('/');
        return LoginPaths.Contains(trimmed);
    }

    private static string GetLoginRedirectUrl(string path)
    {
        var trimmed = path.TrimEnd('/').ToLowerInvariant();

        if (trimmed.StartsWith("/adminportal"))
            return "/AdminPortal?sessionExpired=true";

        if (trimmed.StartsWith("/receptionportal"))
            return "/ReceptionPortal?sessionExpired=true";

        return "/login?sessionExpired=true";
    }
}

public static class AntiforgeryExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseAntiforgeryExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AntiforgeryExceptionMiddleware>();
    }
}
