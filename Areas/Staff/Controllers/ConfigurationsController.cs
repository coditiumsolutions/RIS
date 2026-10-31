using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResidentPortal.Data;
using ResidentPortal.Models;
using ResidentPortal.Security;
using ResidentPortal.Services;

namespace ResidentPortal.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Policy = AuthPolicies.StaffOnly, AuthenticationSchemes = AuthSchemes.Staff)]
[AuthorizePermission(StaffPermissions.SettingsManage)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ConfigurationsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly SmtpSettingsService _smtp;
    private readonly SmsSettingsService _sms;
    private readonly AppSettingsService _appSettings;
    private readonly ComplaintEmailNotifier _email;

    public ConfigurationsController(
        ApplicationDbContext db,
        SmtpSettingsService smtp,
        SmsSettingsService sms,
        AppSettingsService appSettings,
        ComplaintEmailNotifier email)
    {
        _db = db;
        _smtp = smtp;
        _sms = sms;
        _appSettings = appSettings;
        _email = email;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await BuildModelAsync(cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(AdminConfigurationViewModel model, CancellationToken cancellationToken)
    {
        model.Categories = await CategoriesAsync(cancellationToken);
        if (!ModelState.IsValid)
            return View(model);

        await _smtp.SaveAsync(model, cancellationToken);
        await _sms.SaveAsync(model, cancellationToken);
        await _appSettings.SetSettingAsync(AppSettingKeys.ComplaintSlaUrgentHours, model.SlaUrgentHours.ToString(), cancellationToken);
        await _appSettings.SetSettingAsync(AppSettingKeys.ComplaintSlaNormalHours, model.SlaNormalHours.ToString(), cancellationToken);
        await _appSettings.SetSettingAsync(AppSettingKeys.ComplaintSlaLowHours, model.SlaLowHours.ToString(), cancellationToken);

        TempData["Success"] = "Configuration saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendTestEmail(string? testEmail, CancellationToken cancellationToken)
    {
        var to = testEmail;
        if (string.IsNullOrWhiteSpace(to))
        {
            var adminId = User.GetUserId();
            to = await _db.AdminUsers.AsNoTracking()
                .Where(a => a.Uid == adminId)
                .Select(a => a.Email)
                .SingleOrDefaultAsync(cancellationToken);
        }

        var (ok, message) = await _email.SendTestAsync(to ?? string.Empty, cancellationToken);
        TempData[ok ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCategory(ComplaintCategoryEditViewModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            TempData["Error"] = "Category name is required.";
            return RedirectToAction(nameof(Index));
        }

        var name = model.Name.Trim();
        if (await _db.ComplaintCategories.AnyAsync(c => c.Name == name, cancellationToken))
        {
            TempData["Error"] = "That category already exists.";
            return RedirectToAction(nameof(Index));
        }

        _db.ComplaintCategories.Add(new ComplaintCategory
        {
            Name = name,
            IsActive = true,
            SortOrder = model.SortOrder,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Category added.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCategory(int id, CancellationToken cancellationToken)
    {
        var category = await _db.ComplaintCategories.SingleOrDefaultAsync(c => c.Uid == id, cancellationToken);
        if (category is null)
            return NotFound();

        category.IsActive = !category.IsActive;
        await _db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = category.IsActive ? "Category activated." : "Category deactivated.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<AdminConfigurationViewModel> BuildModelAsync(CancellationToken ct)
    {
        var smtp = await _smtp.GetOrCreateAsync(ct);
        var sms = await _sms.GetAsync(ct);
        return new AdminConfigurationViewModel
        {
            SmtpHost = smtp.Host,
            SmtpPort = smtp.Port,
            SmtpUseSsl = smtp.UseSsl,
            SmtpUsername = smtp.Username,
            HasSmtpPassword = !string.IsNullOrWhiteSpace(smtp.PasswordProtected),
            FromEmail = smtp.FromEmail,
            FromName = smtp.FromName,
            EmailNotificationsEnabled = smtp.EmailNotificationsEnabled,
            SlaUrgentHours = await ReadHoursAsync(AppSettingKeys.ComplaintSlaUrgentHours, 24, ct),
            SlaNormalHours = await ReadHoursAsync(AppSettingKeys.ComplaintSlaNormalHours, 72, ct),
            SlaLowHours = await ReadHoursAsync(AppSettingKeys.ComplaintSlaLowHours, 168, ct),
            SmsNotificationsEnabled = sms.Enabled,
            SmsProviderName = sms.ProviderName,
            SmsApiBaseUrl = sms.ApiBaseUrl,
            HasSmsApiKey = sms.HasApiKey,
            SmsSenderId = sms.SenderId,
            SmsExtraSettings = sms.ExtraSettings,
            Categories = await CategoriesAsync(ct)
        };
    }

    private async Task<int> ReadHoursAsync(string key, int fallback, CancellationToken ct)
    {
        var raw = await _appSettings.GetSettingAsync(key, ct);
        return int.TryParse(raw, out var hours) && hours > 0 ? hours : fallback;
    }

    private Task<List<ComplaintCategory>> CategoriesAsync(CancellationToken ct) =>
        _db.ComplaintCategories.AsNoTracking().OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync(ct);
}
