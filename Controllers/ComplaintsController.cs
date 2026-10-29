using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResidentPortal.Models;
using ResidentPortal.Security;

namespace ResidentPortal.Controllers;

[Authorize(Policy = AuthPolicies.ResidentOnly)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ComplaintsController : Controller
{
    public IActionResult Index()
    {
        return View("~/Views/Shared/ModulePlaceholder.cshtml", new ModulePlaceholderViewModel
        {
            Title = "Complaints Management",
            Description = "Submit complaints, track their status, and review previous complaints. This module is coming soon.",
            IconClass = "bi-headset"
        });
    }
}
