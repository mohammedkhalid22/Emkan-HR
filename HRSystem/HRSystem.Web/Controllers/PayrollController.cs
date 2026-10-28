using System.Security.Claims;
using HRSystem.BLL.Services;
using HRSystem.Domain.Identity;
using HRSystem.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRSystem.Web.Controllers;

[Authorize(Roles = Roles.AdminOrHR)]
public class PayrollController : Controller
{
    private const string ExcelType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IPayrollService _payrollService;

    public PayrollController(IPayrollService payrollService)
    {
        _payrollService = payrollService;
    }

    public async Task<IActionResult> Index() => View(await _payrollService.GetRunsAsync());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Calculate(int year, int month)
    {
        var result = await _payrollService.CalculateAsync(year, month);
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = $"مرتبات {month:D2}/{year} اتحسبت. راجعها قبل الاعتماد";
        return RedirectToAction(nameof(Details), new { id = result.Value });
    }

    public async Task<IActionResult> Details(int id)
    {
        var run = await _payrollService.GetRunAsync(id);
        return run is null ? NotFound() : View(run);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var result = await _payrollService.ApproveAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier));
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
            ? "المسير اتعتمد واتقفل، وأقساط السلف اتخصمت، والموظفين يقدروا يشوفوا كشوفهم"
            : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _payrollService.DeleteDraftAsync(id);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "المسودة اتمسحت" : result.Error;
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Register(int id)
    {
        var run = await _payrollService.GetRunAsync(id);
        return run is null ? NotFound()
            : File(PayrollExcelExporter.Register(run), ExcelType, $"payroll-{run.Run.Year}-{run.Run.Month:D2}.xlsx");
    }

    public async Task<IActionResult> BankFile(int id)
    {
        var run = await _payrollService.GetRunAsync(id);
        return run is null ? NotFound()
            : File(PayrollExcelExporter.BankFile(run), ExcelType, $"bank-transfer-{run.Run.Year}-{run.Run.Month:D2}.xlsx");
    }
}
