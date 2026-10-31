using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using ResidentPortal.Data;
using ResidentPortal.Security;
using ResidentPortal.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Data Protection to persist keys across app restarts
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (string.IsNullOrWhiteSpace(keysPath) && OperatingSystem.IsWindows())
{
    keysPath = @"C:\Data\RIS\keys";
}

if (!string.IsNullOrWhiteSpace(keysPath))
{
    var keysDir = new DirectoryInfo(keysPath);
    if (!keysDir.Exists)
        keysDir.Create();

    builder.Services.AddDataProtection()
        .SetApplicationName("RIS")
        .PersistKeysToFileSystem(keysDir);
}
else
{
    builder.Services.AddDataProtection()
        .SetApplicationName("RIS");
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "SQL Server connection string is not configured. Set ConnectionStrings:DefaultConnection with dotnet user-secrets or the ConnectionStrings__DefaultConnection environment variable.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
    {
        sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
        sql.CommandTimeout(30);
    }));

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton<BusinessTimeZone>();
builder.Services.AddScoped<PortalAuthentication>();
builder.Services.AddScoped<AuditLogger>();
builder.Services.AddScoped<DevelopmentAccountSeeder>();
builder.Services.AddScoped<PasswordChangeService>();
builder.Services.AddScoped<AppSettingsService>();
builder.Services.AddScoped<SecurityStampService>();
builder.Services.AddScoped<AccountLockoutService>();
builder.Services.AddSingleton<LoginFailureRateLimiter>();
builder.Services.Configure<VisitorPhotoStorageOptions>(
    builder.Configuration.GetSection(VisitorPhotoStorageOptions.SectionName));
builder.Services.AddSingleton<VisitorPhotoStorage>();
builder.Services.AddScoped<VisitorPhotoService>();
builder.Services.Configure<ComplaintPhotoStorageOptions>(
    builder.Configuration.GetSection(ComplaintPhotoStorageOptions.SectionName));
builder.Services.AddSingleton<ComplaintPhotoStorage>();
builder.Services.AddScoped<SmtpSettingsService>();
builder.Services.AddScoped<ComplaintEmailNotifier>();
builder.Services.AddScoped<ComplaintWorkflow>();
builder.Services.AddScoped<ComplaintQueryService>();
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = Math.Max(options.MultipartBodyLengthLimit, 3 * 1024 * 1024);
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 3 * 1024 * 1024;
});

builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "portal";
        options.DefaultAuthenticateScheme = "portal";
        options.DefaultChallengeScheme = "portal";
    })
    .AddPolicyScheme("portal", "Portal", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var path = context.Request.Path.Value ?? string.Empty;
            if (path.StartsWith("/Staff", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/AdminPortal", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/ReceptionPortal", StringComparison.OrdinalIgnoreCase))
                return AuthSchemes.Staff;
            return AuthSchemes.Resident;
        };
    })
    .AddCookie(AuthSchemes.Resident, options =>
    {
        options.Cookie.Name = "__Host-ResidentPortal-Resident";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Path = "/";
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                var path = context.Request.Path.Value ?? "/";
                if (path.Equals("/login", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.Redirect("/login");
                    return Task.CompletedTask;
                }

                var target = "/login?returnUrl=" + Uri.EscapeDataString(path + context.Request.QueryString);
                context.Response.Redirect(target);
                return Task.CompletedTask;
            },
            OnValidatePrincipal = async context =>
            {
                var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var stamp = context.Principal?.FindFirstValue("SecurityStamp");

                if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(userId, out var uid))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(AuthSchemes.Resident);
                    return;
                }

                var stampService = context.HttpContext.RequestServices.GetRequiredService<SecurityStampService>();
                var (isActive, currentStamp) = await stampService.GetUserStatusAsync("Resident", uid, context.HttpContext.RequestAborted);

                if (!isActive || !string.Equals(stamp, currentStamp, StringComparison.OrdinalIgnoreCase))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(AuthSchemes.Resident);
                }
            }
        };
    })
    .AddCookie(AuthSchemes.Staff, options =>
    {
        options.Cookie.Name = "__Host-ResidentPortal-Staff";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Path = "/";
        options.LoginPath = "/Staff";
        options.AccessDeniedPath = "/Staff/Account/Forbidden";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                var path = context.Request.Path.Value ?? "/Staff";
                if (path.StartsWith("/staff", StringComparison.OrdinalIgnoreCase)
                    && (path.Equals("/staff", StringComparison.OrdinalIgnoreCase)
                        || path.Equals("/staff/", StringComparison.OrdinalIgnoreCase)
                        || path.StartsWith("/staff/account", StringComparison.OrdinalIgnoreCase)))
                {
                    context.Response.Redirect("/Staff");
                    return Task.CompletedTask;
                }

                var target = "/Staff?returnUrl=" + Uri.EscapeDataString(path + context.Request.QueryString);
                context.Response.Redirect(target);
                return Task.CompletedTask;
            },
            OnValidatePrincipal = async context =>
            {
                var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var stamp = context.Principal?.FindFirstValue("SecurityStamp");
                var userType = context.Principal?.FindFirstValue(StaffPermissions.StaffUserTypeClaim);

                if (string.IsNullOrWhiteSpace(userId)
                    || !int.TryParse(userId, out var uid)
                    || string.IsNullOrWhiteSpace(userType)
                    || (!string.Equals(userType, PortalRoles.Admin, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(userType, PortalRoles.Receptionist, StringComparison.OrdinalIgnoreCase)))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(AuthSchemes.Staff);
                    return;
                }

                var stampService = context.HttpContext.RequestServices.GetRequiredService<SecurityStampService>();
                var (isActive, currentStamp) = await stampService.GetUserStatusAsync(userType, uid, context.HttpContext.RequestAborted);

                if (!isActive || !string.Equals(stamp, currentStamp, StringComparison.OrdinalIgnoreCase))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(AuthSchemes.Staff);
                }
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthPolicies.ResidentOnly, policy =>
    {
        policy.AddAuthenticationSchemes(AuthSchemes.Resident);
        policy.RequireAuthenticatedUser();
        policy.RequireRole(PortalRoles.Resident);
    });

    options.AddPolicy(AuthPolicies.StaffOnly, policy =>
    {
        policy.AddAuthenticationSchemes(AuthSchemes.Staff);
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
            context.User.IsInRole(PortalRoles.Admin) || context.User.IsInRole(PortalRoles.Receptionist));
    });

    // Legacy aliases → Staff scheme + role (for any remaining references during migration).
    options.AddPolicy(AuthPolicies.AdminOnly, policy =>
    {
        policy.AddAuthenticationSchemes(AuthSchemes.Staff);
        policy.RequireAuthenticatedUser();
        policy.RequireRole(PortalRoles.Admin);
    });

    options.AddPolicy(AuthPolicies.ReceptionistOnly, policy =>
    {
        policy.AddAuthenticationSchemes(AuthSchemes.Staff);
        policy.RequireAuthenticatedUser();
        policy.RequireRole(PortalRoles.Receptionist);
    });

    options.AddPolicy(AuthPolicies.AdminOrReceptionist, policy =>
    {
        policy.AddAuthenticationSchemes(AuthSchemes.Staff);
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
            context.User.IsInRole(PortalRoles.Admin) || context.User.IsInRole(PortalRoles.Receptionist));
    });

    foreach (var permission in StaffPermissions.All)
    {
        var perm = permission;
        options.AddPolicy(StaffPermissions.PolicyName(perm), policy =>
        {
            policy.AddAuthenticationSchemes(AuthSchemes.Staff);
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(new PermissionRequirement(perm));
        });
    }
});

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "__Host-ResidentPortal-Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/";
});

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
    options.Filters.Add<MustChangePasswordFilter>();
});

// Configure HSTS: 1 year with includeSubDomains (QA-021)
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
    options.Preload = false;
});

// Configure HTTPS redirection: 308 Permanent (QA-022)
builder.Services.AddHttpsRedirection(options =>
{
    options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
    options.HttpsPort = 443;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Friendly 404 page (QA-005)
app.UseStatusCodePagesWithReExecute("/Home/NotFound", "?statusCode={0}");

// Legacy /AdminPortal and /ReceptionPortal → /Staff
app.UseLegacyStaffPortalRedirects();

// Handle antiforgery failures on login forms with friendly redirect
app.UseAntiforgeryExceptionHandler();

app.UseHttpsRedirection();

// Security headers (QA-021 CSP, etc.)
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(self), microphone=(), geolocation=()";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "font-src 'self'; " +
        "img-src 'self' data: blob:; " +
        "media-src 'self' blob:; " +
        "connect-src 'self'; " +
        "frame-ancestors 'none'; " +
        "form-action 'self'; " +
        "base-uri 'self';";
    await next();
});

app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
        name: "areas",
        pattern: "{area:exists}/{controller=Account}/{action=Index}/{id?}");

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

await VerifyDatabaseAsync(app);

app.Run();

static async Task VerifyDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Database");

    try
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            if (!await db.Database.CanConnectAsync())
                throw new InvalidOperationException("Could not connect to SQL Server database ResidentPortal.");
        });
    }
    catch (Exception ex) when (ex is not InvalidOperationException)
    {
        logger.LogError("SQL Server connection failed.");
        throw new InvalidOperationException("Could not connect to SQL Server database ResidentPortal.", ex);
    }

    logger.LogInformation("SQL Server database connection succeeded.");

    var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentAccountSeeder>();
    await seeder.SeedAsync();
}
