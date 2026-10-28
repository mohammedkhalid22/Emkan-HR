using HRSystem.BLL.Services;
using HRSystem.Web.Extensions;
using HRSystem.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace HRSystem.Web.Controllers;

public class PayslipsController : Controller
{
    private readonly IPayrollService _payrollService;
    private readonly IEmployeeService _employeeService;
    private readonly IConfiguration _configuration;

    public PayslipsController(IPayrollService payrollService, IEmployeeService employeeService, IConfiguration configuration)
    {
        _payrollService = payrollService;
        _employeeService = employeeService;
        _configuration = configuration;
    }

    public async Task<IActionResult> Mine()
    {
        var me = await _employeeService.GetApproverAsync(User);
        if (me.EmployeeId is not int employeeId)
        {
            TempData["Error"] = "حسابك مش مربوط بملف موظف";
            return RedirectToAction("Index", "Home");
        }
        return View(await _payrollService.GetMyPayslipsAsync(employeeId));
    }

    // HR: any payslip. Employee: their own, from approved months only.
    public async Task<IActionResult> Download(int id)
    {
        var slip = await _payrollService.GetPayslipAsync(id, await _employeeService.GetApproverAsync(User));
        if (slip is null)
            return NotFound();

        var pdf = PayslipPdf.Generate(slip, _configuration["CompanyName"] ?? "الشركة");
        return File(pdf, "application/pdf", $"payslip-{slip.Item.EmployeeCode}-{slip.Year}-{slip.Month:D2}.pdf");
    }
}
