using HRSystem.BLL.DTOs;
using HRSystem.BLL.Services;
using HRSystem.Domain.Identity;
using HRSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRSystem.Web.Controllers;

[Authorize(Roles = Roles.AdminOrHR)]
public class LeaveBalancesController : Controller
{
    private readonly ILeaveBalanceService _balanceService;
    private readonly IDepartmentService _departmentService;
    private readonly ILeaveTypeService _leaveTypeService;

    public LeaveBalancesController(ILeaveBalanceService balanceService, IDepartmentService departmentService, ILeaveTypeService leaveTypeService)
    {
        _balanceService = balanceService;
        _departmentService = departmentService;
        _leaveTypeService = leaveTypeService;
    }

    public async Task<IActionResult> Index([FromQuery] LeaveBalanceFilterDto filter)
    {
        return View(new LeaveBalancesViewModel
        {
            Filter = filter,
            Rows = await _balanceService.GetAsync(filter),
            Departments = new SelectList(await _departmentService.GetLookupAsync(), "Id", "Name", filter.DepartmentId),
            LeaveTypes = new SelectList(await _leaveTypeService.GetLookupAsync(), "Id", "Name", filter.LeaveTypeId)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(int year)
    {
        var created = await _balanceService.GenerateAsync(year);
        TempData["Success"] = created == 0 ? $"كل أرصدة {year} موجودة بالفعل" : $"اتعمل {created} رصيد لسنة {year}";
        return RedirectToAction(nameof(Index), new { year });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateEntitled(int id, decimal entitled, int year)
    {
        var result = await _balanceService.UpdateEntitledAsync(id, entitled);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "الرصيد اتعدل" : result.Error;
        return RedirectToAction(nameof(Index), new { year });
    }
}
