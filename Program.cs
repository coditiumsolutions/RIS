using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ResidentPortal.Data;
using ResidentPortal.Security;
using ResidentPortal.Services;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddSingleton<PasswordService>();
builder.Services.AddScoped<PortalAuthentication>();
builder.Services.AddScoped<AuditLogger>();
builder.Services.AddScoped<DevelopmentAccountSeeder>();
builder.Services.AddScoped<PasswordChangeService>();

builder.Services.AddAuthentication()
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
        options.Events.OnRedirectToLogin = context =>
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
        };
    })
    .AddCookie(AuthSchemes.Admin, options =>
    {
        options.Cookie.Name = "__Host-ResidentPortal-Admin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Path = "/";
        options.LoginPath = "/adminportal";
        options.AccessDeniedPath = "/adminportal";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.Events.OnRedirectToLogin = context =>
        {
            var path = context.Request.Path.Value ?? "/adminportal";
            if (path.Equals("/adminportal", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/adminportal/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/adminportal/account", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Redirect("/adminportal");
                return Task.CompletedTask;
            }

            var target = "/adminportal?returnUrl=" + Uri.EscapeDataString(path + context.Request.QueryString);
            context.Response.Redirect(target);
            return Task.CompletedTask;
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

    options.AddPolicy(AuthPolicies.AdminOnly, policy =>
    {
        policy.AddAuthenticationSchemes(AuthSchemes.Admin);
        policy.RequireAuthenticatedUser();
        policy.RequireRole(PortalRoles.Admin);
    });
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

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
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
