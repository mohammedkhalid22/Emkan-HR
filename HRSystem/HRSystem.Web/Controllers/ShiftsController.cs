using HRSystem.BLL.DTOs;
using HRSystem.BLL.Services;
using HRSystem.Domain.Identity;
using HRSystem.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRSystem.Web.Controllers;

[Authorize(Roles = Roles.AdminOrHR)]
public class ShiftsController : Controller
{
    private readonly IShiftService _shiftService;

    public ShiftsController(IShiftService shiftService)
    {
        _shiftService = shiftService;
    }

    public async Task<IActionResult> Index()
        => View(await _shiftService.GetAllAsync());

    public IActionResult Create()
        => View(new SaveShiftDto { StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(17, 0) });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SaveShiftDto dto)
    {
        if (!ModelState.IsValid)
            return View(dto);

        var result = await _shiftService.CreateAsync(dto);
        if (!result.Succeeded)
        {
            ModelState.AddServiceError(result);
            return View(dto);
        }

        TempData["Success"] = "الوردية اتضافت";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var dto = await _shiftService.GetForEditAsync(id);
        return dto is null ? NotFound() : View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SaveShiftDto dto)
    {
        if (id != dto.Id)
            return BadRequest();
        if (!ModelState.IsValid)
            return View(dto);

        var result = await _shiftService.UpdateAsync(dto);
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
        var result = await _shiftService.DeleteAsync(id);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "الوردية اتمسحت" : result.Error;
        return RedirectToAction(nameof(Index));
    }
}
