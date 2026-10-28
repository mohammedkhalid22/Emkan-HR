using HRSystem.BLL.DTOs;
using HRSystem.BLL.Services;
using HRSystem.Domain.Identity;
using HRSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRSystem.Web.Controllers;

[Authorize(Roles = Roles.Admin)]
public class SettingsController : Controller
{
    private readonly ISettingsService _settingsService;
    private readonly ITaxBracketService _taxBracketService;

    public SettingsController(ISettingsService settingsService, ITaxBracketService taxBracketService)
    {
        _settingsService = settingsService;
        _taxBracketService = taxBracketService;
    }

    public async Task<IActionResult> Index(int? year)
    {
        var y = year ?? DateTime.Today.Year;
        return View(new SettingsViewModel
        {
            Year = y,
            Settings = await _settingsService.GetAllAsync(),
            Brackets = await _taxBracketService.GetByYearAsync(y),
            NewBracket = new SaveTaxBracketDto { Year = y }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(string key, string value)
    {
        var result = await _settingsService.UpdateAsync(key, value);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "الإعداد اتحفظ" : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddBracket([Bind(Prefix = "NewBracket")] SaveTaxBracketDto dto)
    {
        var result = ModelState.IsValid
            ? await _taxBracketService.AddAsync(dto)
            : BLL.Common.ServiceResult.Failure(ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "الشريحة اتضافت" : result.Error;
        return RedirectToAction(nameof(Index), new { year = dto.Year });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBracket(int id, int year)
    {
        var result = await _taxBracketService.DeleteAsync(id);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "الشريحة اتمسحت" : result.Error;
        return RedirectToAction(nameof(Index), new { year });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CopyYear(int fromYear, int toYear)
    {
        var result = await _taxBracketService.CopyYearAsync(fromYear, toYear);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? $"شرائح {fromYear} اتنسخت لـ {toYear}" : result.Error;
        return RedirectToAction(nameof(Index), new { year = toYear });
    }
}
