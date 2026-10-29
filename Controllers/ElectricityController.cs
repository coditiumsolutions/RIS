using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResidentPortal.Models;
using ResidentPortal.Security;

namespace ResidentPortal.Controllers;

[Authorize(Policy = AuthPolicies.ResidentOnly)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ElectricityController : Controller
{
    public IActionResult Index()
    {
        return View("~/Views/Shared/ModulePlaceholder.cshtml", new ModulePlaceholderViewModel
        {
            Title = "Electricity Management",
            Description = "View electricity bills, payment details, meter information, and billing history. This module is coming soon.",
            IconClass = "bi-lightning-charge-fill"
        });
    }
}
