using HRSystem.BLL.Services;
using HRSystem.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace HRSystem.Web.Services;

// When the "RequireTwoFactorForAdmins" setting is 1, an Admin or HR user without two-step sign-in
// is sent to set it up before reaching any other page (they see payroll and every employee's data)
public class TwoFactorRequiredFilter : IAsyncActionFilter
{
    public const string SettingKey = "RequireTwoFactorForAdmins";

    // Setting it up, signing out and the bell's polling must still work
    private static readonly string[] OpenControllers = ["Account", "Notifications"];

    private readonly ISettingsService _settings;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ITempDataDictionaryFactory _tempData;

    public TwoFactorRequiredFilter(ISettingsService settings, UserManager<ApplicationUser> users, ITempDataDictionaryFactory tempData)
    {
        _settings = settings;
        _users = users;
        _tempData = tempData;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var principal = context.HttpContext.User;
        var controller = (context.ActionDescriptor as ControllerActionDescriptor)?.ControllerName;

        if (principal.Identity?.IsAuthenticated == true
            && (principal.IsInRole(Roles.Admin) || principal.IsInRole(Roles.HR))
            && !OpenControllers.Contains(controller)
            && await _settings.GetIntAsync(SettingKey, 0) == 1
            && await _users.GetUserAsync(principal) is { TwoFactorEnabled: false })
        {
            _tempData.GetTempData(context.HttpContext)["Error"] = "لازم تفعّل التحقق بخطوتين الأول عشان تكمّل. ده إعداد الشركة لحسابات الأدمن والـ HR";
            context.Result = new RedirectToActionResult("EnableTwoFactor", "Account", null);
            return;
        }

        await next();
    }
}
