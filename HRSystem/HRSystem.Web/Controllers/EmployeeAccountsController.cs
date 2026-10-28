using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.BLL.Services;
using HRSystem.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRSystem.Web.Controllers;

[Authorize(Roles = Roles.Admin)]
public class EmployeeAccountsController : Controller
{
    private readonly IEmployeeAccountService _accountService;

    public EmployeeAccountsController(IEmployeeAccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = "NewAccount")] CreateAccountDto dto)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage;
            return BackToAccount(dto.EmployeeId);
        }

        return Done(await _accountService.CreateAsync(dto), dto.EmployeeId, "الحساب اتعمل");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(int employeeId, string role)
        => Done(await _accountService.ChangeRoleAsync(employeeId, role), employeeId, "الدور اتغير");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(int employeeId, bool isActive)
        => Done(await _accountService.SetActiveAsync(employeeId, isActive), employeeId,
            isActive ? "الحساب اتفعّل" : "الحساب اتوقف");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(int employeeId, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            TempData["Error"] = "اكتب كلمة السر الجديدة";
            return BackToAccount(employeeId);
        }

        return Done(await _accountService.ResetPasswordAsync(employeeId, newPassword), employeeId, "كلمة السر اتغيرت");
    }

    private IActionResult Done(ServiceResult result, int employeeId, string successMessage)
    {
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? successMessage : result.Error;
        return BackToAccount(employeeId);
    }

    private IActionResult BackToAccount(int employeeId)
        => RedirectToAction("Details", "Employees", new { id = employeeId }, "account");
}
