using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.BLL.Services;
using HRSystem.Domain.Identity;
using HRSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRSystem.Web.Controllers;

[Authorize(Roles = Roles.AdminOrHR)]
public class LoansController : Controller
{
    private readonly ILoanService _loanService;
    private readonly IEmployeeService _employeeService;

    public LoansController(ILoanService loanService, IEmployeeService employeeService)
    {
        _loanService = loanService;
        _employeeService = employeeService;
    }

    public async Task<IActionResult> Index(bool all = false)
    {
        var today = DateTime.Today;
        return View(new LoansViewModel
        {
            ShowAll = all,
            Loans = await _loanService.GetAllAsync(activeOnly: !all),
            Employees = new SelectList(await _employeeService.GetManagerLookupAsync(null), "Id", "Name"),
            NewLoan = new CreateLoanDto { StartMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(1), InstallmentsCount = 6 }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = "NewLoan")] CreateLoanDto dto)
    {
        var result = ModelState.IsValid
            ? await _loanService.CreateAsync(dto)
            : ServiceResult.Failure(ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "السلفة اتسجلت" : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var result = await _loanService.CancelAsync(id);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "السلفة اتلغت" : result.Error;
        return RedirectToAction(nameof(Index));
    }
}
