using HRSystem.BLL.DTOs;
using HRSystem.BLL.Services;
using HRSystem.Domain.Identity;
using HRSystem.Web.Extensions;
using HRSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRSystem.Web.Controllers;

[Authorize(Roles = Roles.AdminOrHR)]
public class PublicHolidaysController : Controller
{
    private readonly IPublicHolidayService _holidayService;

    public PublicHolidaysController(IPublicHolidayService holidayService)
    {
        _holidayService = holidayService;
    }

    public async Task<IActionResult> Index(int? year)
    {
        var selectedYear = year ?? DateTime.Today.Year;
        return View(new HolidaysViewModel
        {
            Year = selectedYear,
            Holidays = await _holidayService.GetByYearAsync(selectedYear),
            NewHoliday = new SavePublicHolidayDto { Date = new DateOnly(selectedYear, DateTime.Today.Month, 1) }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = "NewHoliday")] SavePublicHolidayDto dto)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage;
            return RedirectToAction(nameof(Index), new { year = dto.Date?.Year });
        }

        var result = await _holidayService.CreateAsync(dto);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "العطلة اتضافت" : result.Error;
        return RedirectToAction(nameof(Index), new { year = dto.Date!.Value.Year });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var dto = await _holidayService.GetForEditAsync(id);
        return dto is null ? NotFound() : View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SavePublicHolidayDto dto)
    {
        if (id != dto.Id)
            return BadRequest();
        if (!ModelState.IsValid)
            return View(dto);

        var result = await _holidayService.UpdateAsync(dto);
        if (!result.Succeeded)
        {
            ModelState.AddServiceError(result);
            return View(dto);
        }

        TempData["Success"] = "التعديلات اتحفظت";
        return RedirectToAction(nameof(Index), new { year = dto.Date!.Value.Year });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, int year)
    {
        var result = await _holidayService.DeleteAsync(id);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "العطلة اتمسحت" : result.Error;
        return RedirectToAction(nameof(Index), new { year });
    }
}
