using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResidentPortal.Models;
using ResidentPortal.Security;

namespace ResidentPortal.Controllers;

[Authorize(Policy = AuthPolicies.ResidentOnly)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class GatePassController : Controller
{
    public IActionResult Index()
    {
        return View("~/Views/Shared/ModulePlaceholder.cshtml", new ModulePlaceholderViewModel
        {
            Title = "Gate Pass",
            Description = "Request and manage residential gate passes. This module is coming soon.",
            IconClass = "bi-door-open-fill"
        });
    }
}
