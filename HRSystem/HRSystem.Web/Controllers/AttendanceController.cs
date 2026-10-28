using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.BLL.Services;
using HRSystem.Domain.Identity;
using HRSystem.Web.Extensions;
using HRSystem.Web.Services;
using HRSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRSystem.Web.Controllers;

public class AttendanceController : Controller
{
    private const string ImportFolder = "imports";

    private readonly IAttendanceService _attendanceService;
    private readonly IEmployeeService _employeeService;
    private readonly IDepartmentService _departmentService;
    private readonly IFileStorage _fileStorage;

    public AttendanceController(IAttendanceService attendanceService, IEmployeeService employeeService,
        IDepartmentService departmentService, IFileStorage fileStorage)
    {
        _attendanceService = attendanceService;
        _employeeService = employeeService;
        _departmentService = departmentService;
        _fileStorage = fileStorage;
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    // ---------- Records (HR: all, manager: team, employee: own) ----------

    public async Task<IActionResult> Index([FromQuery] AttendanceFilterDto filter)
    {
        var scope = await _employeeService.GetScopeAsync(User);

        // Plain employees see their month by default; others see today
        filter.From ??= scope.SeeAll || scope.SeeTeam ? Today : new DateOnly(Today.Year, Today.Month, 1);
        filter.To ??= Today;

        return View(new AttendanceIndexViewModel
        {
            Filter = filter,
            Result = await _attendanceService.GetRecordsAsync(filter, scope),
            Departments = new SelectList(await _departmentService.GetLookupAsync(), "Id", "Name", filter.DepartmentId),
            ShowEmployee = scope.SeeAll || scope.SeeTeam,
            CanEdit = User.IsAdminOrHR(),
            Today = await MyTodayAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckIn(string? returnUrl)
    {
        var me = await _employeeService.GetApproverAsync(User);
        var result = me.EmployeeId is int id
            ? await _attendanceService.CheckInAsync(id)
            : ServiceResult.Failure("حسابك مش مربوط بملف موظف");
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "اتسجل حضورك" : result.Error;
        return Back(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckOut(string? returnUrl)
    {
        var me = await _employeeService.GetApproverAsync(User);
        var result = me.EmployeeId is int id
            ? await _attendanceService.CheckOutAsync(id)
            : ServiceResult.Failure("حسابك مش مربوط بملف موظف");
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "اتسجل انصرافك" : result.Error;
        return Back(returnUrl);
    }

    // ---------- Manual edit (HR) ----------

    [Authorize(Roles = Roles.AdminOrHR)]
    public async Task<IActionResult> Edit(int employeeId, DateOnly date, string? returnUrl)
    {
        var dto = await _attendanceService.GetForEditAsync(employeeId, date);
        if (dto is null)
            return NotFound();

        ViewData["ReturnUrl"] = returnUrl;
        return View(dto);
    }

    [Authorize(Roles = Roles.AdminOrHR)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ManualAttendanceDto dto, string? returnUrl)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
            return View(dto);

        var result = await _attendanceService.SaveManualAsync(dto);
        if (!result.Succeeded)
        {
            ModelState.AddServiceError(result);
            return View(dto);
        }

        TempData["Success"] = "التسجيل اتحفظ";
        return Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction(nameof(Index), new { From = dto.Date, To = dto.Date });
    }

    [Authorize(Roles = Roles.AdminOrHR)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseDays(DateOnly from, DateOnly to)
    {
        var changed = await _attendanceService.CloseDaysAsync(from, to);
        TempData["Success"] = $"اتقفلت الأيام من {from:yyyy-MM-dd} لـ {to:yyyy-MM-dd} ({changed} تسجيل اتعمل أو اتحدّث)";
        return RedirectToAction(nameof(Index), new { From = from, To = to });
    }

    // ---------- Monthly report (HR) ----------

    [Authorize(Roles = Roles.AdminOrHR)]
    public async Task<IActionResult> Monthly(int? year, int? month, int? departmentId)
    {
        var y = year ?? Today.Year;
        var m = month ?? Today.Month;
        return View(new MonthlyAttendanceViewModel
        {
            Year = y,
            Month = m,
            DepartmentId = departmentId,
            Rows = await _attendanceService.GetMonthlyReportAsync(y, m, departmentId),
            Departments = new SelectList(await _departmentService.GetLookupAsync(), "Id", "Name", departmentId)
        });
    }

    [Authorize(Roles = Roles.AdminOrHR)]
    public async Task<IActionResult> ExportMonthly(int year, int month, int? departmentId)
    {
        var rows = await _attendanceService.GetMonthlyReportAsync(year, month, departmentId);
        return File(AttendanceExcelExporter.ExportMonthly(rows, year, month),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"attendance-{year}-{month:D2}.xlsx");
    }

    // ---------- Fingerprint import (HR) ----------

    [Authorize(Roles = Roles.AdminOrHR)]
    public IActionResult Import() => View(new AttendanceImportViewModel());

    // Step 1: upload and preview, nothing is saved yet
    [Authorize(Roles = Roles.AdminOrHR)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile? file)
    {
        var upload = file.ToFileUpload();
        var extension = Path.GetExtension(file?.FileName ?? "").ToLowerInvariant();
        if (upload is null || !AttendanceFileReader.Extensions.Contains(extension))
        {
            TempData["Error"] = "اختار ملف CSV أو Excel (.xlsx)";
            return RedirectToAction(nameof(Import));
        }
        if (upload.Length > FileRules.MaxBytes)
        {
            TempData["Error"] = "حجم الملف أكبر من 5 ميجا";
            return RedirectToAction(nameof(Import));
        }

        var token = await _fileStorage.SaveAsync(upload, ImportFolder);
        var results = await ReadAndImportAsync(token, apply: false);
        if (results is null)
        {
            TempData["Error"] = "الملف مش مقروء. اتأكد إنه بنفس شكل الملف النموذجي";
            return RedirectToAction(nameof(Import));
        }

        return View(new AttendanceImportViewModel { Token = token, FileName = file!.FileName, Results = results });
    }

    // Step 2: save the valid rows
    [Authorize(Roles = Roles.AdminOrHR)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmImport(string token)
    {
        // The token is a path we generated; refuse anything else
        if (!token.StartsWith(ImportFolder + "/", StringComparison.Ordinal) || token.Contains(".."))
            return BadRequest();

        var results = await ReadAndImportAsync(token, apply: true);
        _fileStorage.Delete(token);

        if (results is null)
        {
            TempData["Error"] = "الملف مش موجود. ارفعه تاني";
            return RedirectToAction(nameof(Import));
        }

        var saved = results.Count(r => r.IsValid);
        TempData["Success"] = $"اتحفظ {saved} تسجيل" + (results.Count > saved ? $"، واتجاهل {results.Count - saved} صف فيه مشكلة" : "");
        var dates = results.Where(r => r.IsValid).Select(r => r.Date!.Value).ToList();
        return dates.Count == 0
            ? RedirectToAction(nameof(Import))
            : RedirectToAction(nameof(Index), new { From = dates.Min(), To = dates.Max() });
    }

    [Authorize(Roles = Roles.AdminOrHR)]
    public IActionResult Sample()
        => File(System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(AttendanceFileReader.SampleCsv)).ToArray(),
            "text/csv", "attendance-sample.csv");

    private async Task<List<AttendanceImportResultDto>?> ReadAndImportAsync(string token, bool apply)
    {
        await using var stream = _fileStorage.Open(token);
        if (stream is null)
            return null;

        try
        {
            var rows = AttendanceFileReader.Read(stream, token);
            return await _attendanceService.ImportAsync(rows, apply);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A broken or non-Excel file
            return null;
        }
    }

    // ---------- Helpers ----------

    private async Task<TodayAttendanceDto?> MyTodayAsync()
    {
        var me = await _employeeService.GetApproverAsync(User);
        return me.EmployeeId is int id ? await _attendanceService.GetTodayAsync(id) : null;
    }

    private IActionResult Back(string? returnUrl)
        => Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction(nameof(Index));
}
