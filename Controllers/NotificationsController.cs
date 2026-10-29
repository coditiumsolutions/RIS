using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResidentPortal.Models;
using ResidentPortal.Security;

namespace ResidentPortal.Controllers;

[Authorize(Policy = AuthPolicies.ResidentOnly)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class NotificationsController : Controller
{
    public IActionResult Index()
    {
        return View("~/Views/Shared/ModulePlaceholder.cshtml", new ModulePlaceholderViewModel
        {
            Title = "Society Notifications",
            Description = "View society announcements, important notices, maintenance updates, and community alerts. This module will be available in a later release.",
            IconClass = "bi-bell-fill"
        });
    }
}
