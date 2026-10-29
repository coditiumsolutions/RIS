using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResidentPortal.Models;
using ResidentPortal.Security;

namespace ResidentPortal.Controllers;

[Authorize(Policy = AuthPolicies.ResidentOnly)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class BillManagementController : Controller
{
    public IActionResult Index()
    {
        return View("~/Views/Shared/ModulePlaceholder.cshtml", new ModulePlaceholderViewModel
        {
            Title = "Bill Management",
            Description = "Review and pay society bills. This module is coming soon.",
            IconClass = "bi-receipt"
        });
    }
}
