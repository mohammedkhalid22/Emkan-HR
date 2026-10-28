using HRSystem.BLL.DTOs;
using HRSystem.BLL.Services;
using HRSystem.Domain.Identity;
using HRSystem.Web.Extensions;
using HRSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRSystem.Web.Controllers;

public class LeaveRequestsController : Controller
{
    private readonly ILeaveRequestService _requestService;
    private readonly ILeaveBalanceService _balanceService;
    private readonly ILeaveTypeService _leaveTypeService;
    private readonly IEmployeeService _employeeService;
    private readonly IDepartmentService _departmentService;
    private readonly IAttendanceRequestService _attendanceRequestService;

    public LeaveRequestsController(ILeaveRequestService requestService, ILeaveBalanceService balanceService,
        ILeaveTypeService leaveTypeService, IEmployeeService employeeService, IDepartmentService departmentService,
        IAttendanceRequestService attendanceRequestService)
    {
        _requestService = requestService;
        _balanceService = balanceService;
        _leaveTypeService = leaveTypeService;
        _employeeService = employeeService;
        _departmentService = departmentService;
        _attendanceRequestService = attendanceRequestService;
    }

    // ---------- Employee ----------

    public async Task<IActionResult> Mine()
    {
        var me = await _employeeService.GetApproverAsync(User);
        if (me.EmployeeId is not int employeeId)
            return NoEmployeeRecord();

        return View(new MyLeavesViewModel
        {
            Balances = await _balanceService.GetForEmployeeAsync(employeeId, DateTime.Today.Year),
            Requests = await _requestService.GetForEmployeeAsync(employeeId, me)
        });
    }

    public async Task<IActionResult> Create()
    {
        var me = await _employeeService.GetApproverAsync(User);
        if (me.EmployeeId is not int employeeId)
            return NoEmployeeRecord();

        var model = new LeaveRequestFormViewModel();
        await FillFormAsync(model, employeeId);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LeaveRequestFormViewModel model)
    {
        var me = await _employeeService.GetApproverAsync(User);
        if (me.EmployeeId is not int employeeId)
            return NoEmployeeRecord();

        if (ModelState.IsValid)
        {
            var result = await _requestService.SubmitAsync(employeeId, model, model.Attachment.ToFileUpload());
            if (result.Succeeded)
            {
                TempData["Success"] = "طلب الإجازة اتبعت";
                return RedirectToAction(nameof(Mine));
            }
            ModelState.AddServiceError(result);
        }

        await FillFormAsync(model, employeeId);
        return View(model);
    }

    // Called from the form while typing dates, to show the working days and remaining balance
    public async Task<IActionResult> Preview(int leaveTypeId, DateOnly start, DateOnly end)
    {
        var me = await _employeeService.GetApproverAsync(User);
        if (me.EmployeeId is not int employeeId)
            return Json(new LeavePreviewDto { Error = "حسابك مش مربوط بملف موظف" });

        return Json(await _requestService.PreviewAsync(employeeId, leaveTypeId, start, end));
    }

    // ---------- Approvers ----------

    [Authorize(Roles = Roles.Admin + "," + Roles.HR + "," + Roles.Manager)]
    public async Task<IActionResult> Awaiting()
    {
        var me = await _employeeService.GetApproverAsync(User);
        return View(new AwaitingViewModel
        {
            Leaves = await _requestService.GetAwaitingAsync(me),
            AttendanceRequests = await _attendanceRequestService.GetAwaitingAsync(me)
        });
    }

    [Authorize(Roles = Roles.AdminOrHR)]
    public async Task<IActionResult> Index([FromQuery] LeaveRequestFilterDto filter)
    {
        var me = await _employeeService.GetApproverAsync(User);
        filter.Year ??= DateTime.Today.Year;

        return View(new LeaveRequestsIndexViewModel
        {
            Filter = filter,
            Result = await _requestService.GetPagedAsync(filter, me),
            Departments = new SelectList(await _departmentService.GetLookupAsync(), "Id", "Name", filter.DepartmentId)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? returnUrl)
    {
        var result = await _requestService.ApproveAsync(id, await _employeeService.GetApproverAsync(User));
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "الطلب اتوافق عليه" : result.Error;
        return Back(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? reason, string? returnUrl)
    {
        var result = await _requestService.RejectAsync(id, await _employeeService.GetApproverAsync(User), reason ?? "");
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "الطلب اترفض" : result.Error;
        return Back(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? returnUrl)
    {
        var result = await _requestService.CancelAsync(id, await _employeeService.GetApproverAsync(User));
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "الطلب اتلغى" : result.Error;
        return Back(returnUrl);
    }

    public async Task<IActionResult> Attachment(int id)
    {
        var file = await _requestService.OpenAttachmentAsync(id, await _employeeService.GetApproverAsync(User));
        return file is null ? NotFound() : File(file.Content, file.ContentType, file.FileName);
    }

    // ---------- Helpers ----------

    private IActionResult Back(string? returnUrl)
        => Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction(nameof(Mine));

    private IActionResult NoEmployeeRecord()
    {
        TempData["Error"] = "حسابك مش مربوط بملف موظف، فمينفعش تقدم إجازات";
        return RedirectToAction("Index", "Home");
    }

    private async Task FillFormAsync(LeaveRequestFormViewModel model, int employeeId)
    {
        model.LeaveTypes = new SelectList(await _leaveTypeService.GetLookupAsync(), "Id", "Name", model.LeaveTypeId);
        model.Balances = await _balanceService.GetForEmployeeAsync(employeeId, DateTime.Today.Year);
    }
}
