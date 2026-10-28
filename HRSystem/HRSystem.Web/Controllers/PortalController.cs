using HRSystem.BLL.Services;
using HRSystem.Web.Extensions;
using HRSystem.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace HRSystem.Web.Controllers;

// Everything about the logged-in employee on one page
public class PortalController : Controller
{
    private readonly IEmployeeService _employeeService;
    private readonly IAttendanceService _attendanceService;
    private readonly ILeaveBalanceService _balanceService;
    private readonly ILeaveRequestService _leaveRequestService;
    private readonly IPayrollService _payrollService;
    private readonly ILoanService _loanService;

    public PortalController(IEmployeeService employeeService, IAttendanceService attendanceService,
        ILeaveBalanceService balanceService, ILeaveRequestService leaveRequestService,
        IPayrollService payrollService, ILoanService loanService)
    {
        _employeeService = employeeService;
        _attendanceService = attendanceService;
        _balanceService = balanceService;
        _leaveRequestService = leaveRequestService;
        _payrollService = payrollService;
        _loanService = loanService;
    }

    public async Task<IActionResult> Index()
    {
        var me = await _employeeService.GetApproverAsync(User);
        if (me.EmployeeId is not int id)
        {
            TempData["Error"] = "حسابك مش مربوط بملف موظف";
            return RedirectToAction("Index", "Home");
        }

        var today = DateTime.Today;
        var employee = await _employeeService.GetDetailsAsync(id, await _employeeService.GetScopeAsync(User));
        var month = await _attendanceService.GetMonthlyReportAsync(today.Year, today.Month, null);

        return View(new PortalViewModel
        {
            Employee = employee!,
            Today = await _attendanceService.GetTodayAsync(id),
            Balances = await _balanceService.GetForEmployeeAsync(id, today.Year),
            ThisMonth = month.FirstOrDefault(r => r.EmployeeId == id),
            RecentLeaves = (await _leaveRequestService.GetForEmployeeAsync(id, me)).Take(5).ToList(),
            Payslips = (await _payrollService.GetMyPayslipsAsync(id)).Take(6).ToList(),
            Loans = (await _loanService.GetForEmployeeAsync(id)).Where(l => l.Status == Domain.Enums.LoanStatus.Active).ToList()
        });
    }
}
