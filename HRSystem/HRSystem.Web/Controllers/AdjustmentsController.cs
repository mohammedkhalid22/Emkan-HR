using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.BLL.Services;
using HRSystem.Domain.Enums;
using HRSystem.Domain.Identity;
using HRSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRSystem.Web.Controllers;

// Bonuses and penalties per month
[Authorize(Roles = Roles.AdminOrHR)]
public class AdjustmentsController : Controller
{
    private readonly IPayrollAdjustmentService _adjustmentService;
    private readonly IEmployeeService _employeeService;

    public AdjustmentsController(IPayrollAdjustmentService adjustmentService, IEmployeeService employeeService)
    {
        _adjustmentService = adjustmentService;
        _employeeService = employeeService;
    }

    public async Task<IActionResult> Index(int? year, int? month)
    {
        var y = year ?? DateTime.Today.Year;
        var m = month ?? DateTime.Today.Month;
        return View(new AdjustmentsViewModel
        {
            Year = y,
            Month = m,
            Items = await _adjustmentService.GetForMonthAsync(y, m),
            Employees = new SelectList(await _employeeService.GetManagerLookupAsync(null), "Id", "Name"),
            NewAdjustment = new CreateAdjustmentDto { Year = y, Month = m, Type = AdjustmentType.Bonus }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = "NewAdjustment")] CreateAdjustmentDto dto)
    {
        var result = ModelState.IsValid
            ? await _adjustmentService.CreateAsync(dto)
            : ServiceResult.Failure(ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "اتسجل" : result.Error;
        return RedirectToAction(nameof(Index), new { year = dto.Year, month = dto.Month });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, int year, int month)
    {
        var result = await _adjustmentService.DeleteAsync(id);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "اتمسح" : result.Error;
        return RedirectToAction(nameof(Index), new { year, month });
    }
}
