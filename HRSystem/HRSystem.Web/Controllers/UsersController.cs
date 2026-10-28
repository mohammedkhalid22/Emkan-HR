using System.Security.Claims;
using HRSystem.BLL.Common;
using HRSystem.BLL.Services;
using HRSystem.Domain.Identity;
using HRSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRSystem.Web.Controllers;

[Authorize(Roles = Roles.Admin)]
public class UsersController : Controller
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    public async Task<IActionResult> Index()
        => View(new UsersViewModel { Users = await _userService.GetAllAsync(), CurrentUserId = CurrentUserId });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = "NewUser")] CreateUserDto dto)
    {
        var result = ModelState.IsValid
            ? await _userService.CreateAsync(dto)
            : ServiceResult.Failure(ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage);
        return Done(result, "الحساب اتعمل");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(string id, string role)
        => Done(await _userService.ChangeRoleAsync(id, role, CurrentUserId), "الدور اتغير");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(string id, bool isActive)
        => Done(await _userService.SetActiveAsync(id, isActive, CurrentUserId), isActive ? "الحساب اتفعّل" : "الحساب اتوقف");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string id, string newPassword)
        => Done(await _userService.ResetPasswordAsync(id, newPassword), "كلمة السر اتغيرت");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(string id)
        => Done(await _userService.UnlockAsync(id), "الحساب اتفتح");

    private IActionResult Done(ServiceResult result, string success)
    {
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? success : result.Error;
        return RedirectToAction(nameof(Index));
    }
}
