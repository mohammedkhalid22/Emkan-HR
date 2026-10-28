using HRSystem.BLL.DTOs;
using HRSystem.BLL.Services;
using HRSystem.Domain.Identity;
using HRSystem.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRSystem.Web.Controllers;

[Authorize(Roles = Roles.AdminOrHR)]
public class LeaveTypesController : Controller
{
    private readonly ILeaveTypeService _leaveTypeService;

    public LeaveTypesController(ILeaveTypeService leaveTypeService)
    {
        _leaveTypeService = leaveTypeService;
    }

    public async Task<IActionResult> Index()
        => View(await _leaveTypeService.GetAllAsync());

    public IActionResult Create() => View(new SaveLeaveTypeDto());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SaveLeaveTypeDto dto)
    {
        if (!ModelState.IsValid)
            return View(dto);

        var result = await _leaveTypeService.CreateAsync(dto);
        if (!result.Succeeded)
        {
            ModelState.AddServiceError(result);
            return View(dto);
        }

        TempData["Success"] = "نوع الإجازة اتضاف";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var dto = await _leaveTypeService.GetForEditAsync(id);
        return dto is null ? NotFound() : View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SaveLeaveTypeDto dto)
    {
        if (id != dto.Id)
            return BadRequest();
        if (!ModelState.IsValid)
            return View(dto);

        var result = await _leaveTypeService.UpdateAsync(dto);
        if (!result.Succeeded)
        {
            ModelState.AddServiceError(result);
            return View(dto);
        }

        TempData["Success"] = "التعديلات اتحفظت";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _leaveTypeService.DeleteAsync(id);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "نوع الإجازة اتمسح" : result.Error;
        return RedirectToAction(nameof(Index));
    }
}
