using Microsoft.AspNetCore.Mvc;
using ResourceManagementSystem.MVC.Security;
using ResourceManagementSystem.Services.Common;
using ResourceManagementSystem.Utilities.Filters;

namespace ResourceManagementSystem.MVC.Controllers;

/// <summary>Small base class: the current actor and a flash message for Post/Redirect/Get.</summary>
public abstract class ApexPostController : Controller
{
    protected StaffContext Actor => User.ToStaffContext();

    protected void Flash(string message) => TempData["Success"] = message;

    protected void FlashError(string message) => TempData[DomainExceptionFilter.ErrorTempDataKey] = message;
}
