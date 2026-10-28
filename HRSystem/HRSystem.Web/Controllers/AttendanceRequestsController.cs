using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.BLL.Services;
using HRSystem.Domain.Enums;
using HRSystem.Web.Extensions;
using HRSystem.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace HRSystem.Web.Controllers;

// Permissions and missions; approvers see them in LeaveRequests/Awaiting next to leave requests
public class AttendanceRequestsController : Controller
{
    private readonly IAttendanceRequestService _requestService;
    private readonly IEmployeeService _employeeService;

    public AttendanceRequestsController(IAttendanceRequestService requestService, IEmployeeService employeeService)
    {
        _requestService = requestService;
        _employeeService = employeeService;
    }

    public async Task<IActionResult> Mine()
    {
        var me = await _employeeService.GetApproverAsync(User);
        if (me.EmployeeId is not int employeeId)
            return NoEmployeeRecord();

        return View(new MyAttendanceRequestsViewModel
        {
            Requests = await _requestService.GetForEmployeeAsync(employeeId, me),
            NewRequest = new CreateAttendanceRequestDto
            {
                Type = AttendanceRequestType.Permission,
                Date = DateOnly.FromDateTime(DateTime.Now)
            }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = "NewRequest")] CreateAttendanceRequestDto dto)
    {
        var me = await _employeeService.GetApproverAsync(User);
        if (me.EmployeeId is not int employeeId)
            return NoEmployeeRecord();

        var result = ModelState.IsValid
            ? await _requestService.SubmitAsync(employeeId, dto)
            : ServiceResult.Failure(ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage);

        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "الطلب اتبعت" : result.Error;
        return RedirectToAction(nameof(Mine));
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

    private IActionResult Back(string? returnUrl)
        => Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction(nameof(Mine));

    private IActionResult NoEmployeeRecord()
    {
        TempData["Error"] = "حسابك مش مربوط بملف موظف";
        return RedirectToAction("Index", "Home");
    }
}
