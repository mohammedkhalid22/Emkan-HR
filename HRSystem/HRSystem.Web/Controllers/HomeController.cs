using System.Diagnostics;
using HRSystem.BLL.Services;
using HRSystem.Web.Extensions;
using HRSystem.Web.Models;
using HRSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRSystem.Web.Controllers;

public class HomeController : Controller
{
    private readonly IBranchService _branchService;
    private readonly IDepartmentService _departmentService;
    private readonly IJobTitleService _jobTitleService;
    private readonly IEmployeeService _employeeService;
    private readonly ILeaveRequestService _leaveRequestService;
    private readonly ILeaveBalanceService _leaveBalanceService;
    private readonly IAttendanceRequestService _attendanceRequestService;
    private readonly IAttendanceService _attendanceService;
    private readonly IDashboardService _dashboardService;

    public HomeController(IBranchService branchService, IDepartmentService departmentService,
        IJobTitleService jobTitleService, IEmployeeService employeeService,
        ILeaveRequestService leaveRequestService, ILeaveBalanceService leaveBalanceService,
        IAttendanceRequestService attendanceRequestService, IAttendanceService attendanceService,
        IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
        _branchService = branchService;
        _departmentService = departmentService;
        _jobTitleService = jobTitleService;
        _employeeService = employeeService;
        _leaveRequestService = leaveRequestService;
        _leaveBalanceService = leaveBalanceService;
        _attendanceRequestService = attendanceRequestService;
        _attendanceService = attendanceService;
    }

    public async Task<IActionResult> Index()
    {
        var me = await _employeeService.GetApproverAsync(User);

        var model = new DashboardViewModel
        {
            UserName = User.Identity?.Name ?? "",
            BranchesCount = (await _branchService.GetLookupAsync()).Count,
            DepartmentsCount = (await _departmentService.GetLookupAsync()).Count,
            JobTitlesCount = (await _jobTitleService.GetLookupAsync()).Count,
            EmployeesCount = await _employeeService.CountActiveAsync(),
            CanApprove = me.IsHr || me.IsManager,
            AwaitingCount = me.IsHr || me.IsManager
                ? await _leaveRequestService.CountAwaitingAsync(me) + await _attendanceRequestService.CountAwaitingAsync(me)
                : 0,
            MyBalances = me.EmployeeId is int employeeId
                ? await _leaveBalanceService.GetForEmployeeAsync(employeeId, DateTime.Today.Year)
                : [],
            Today = me.EmployeeId is int id ? await _attendanceService.GetTodayAsync(id) : null,
            Hr = me.IsHr ? await _dashboardService.GetHrDashboardAsync() : null
        };

        return View(model);
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
