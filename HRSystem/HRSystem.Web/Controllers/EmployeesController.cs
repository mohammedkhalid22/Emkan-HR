using HRSystem.BLL.DTOs;
using HRSystem.BLL.Services;
using HRSystem.Domain.Identity;
using HRSystem.Web.Extensions;
using HRSystem.Web.Services;
using HRSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using HRSystem.BLL.Common;

namespace HRSystem.Web.Controllers;

public class EmployeesController : Controller
{
    private readonly IEmployeeService _employeeService;
    private readonly IEmployeeAccountService _accountService;
    private readonly IDepartmentService _departmentService;
    private readonly IJobTitleService _jobTitleService;
    private readonly IFileStorage _fileStorage;
    private readonly ISalaryComponentService _componentService;
    private readonly ILoanService _loanService;

    public EmployeesController(
        IEmployeeService employeeService,
        IEmployeeAccountService accountService,
        IDepartmentService departmentService,
        IJobTitleService jobTitleService,
        IFileStorage fileStorage,
        ISalaryComponentService componentService,
        ILoanService loanService)
    {
        _employeeService = employeeService;
        _accountService = accountService;
        _departmentService = departmentService;
        _jobTitleService = jobTitleService;
        _fileStorage = fileStorage;
        _componentService = componentService;
        _loanService = loanService;
    }

    public async Task<IActionResult> Index([FromQuery] EmployeeFilterDto filter)
    {
        // A plain employee has no list to browse, only their own profile
        if (!User.IsAdminOrHR() && !User.IsInRole(Roles.Manager))
            return RedirectToAction(nameof(Me));

        var scope = await _employeeService.GetScopeAsync(User);
        var model = new EmployeeIndexViewModel
        {
            Filter = filter,
            Result = await _employeeService.GetPagedAsync(filter, scope),
            Departments = new SelectList(await _departmentService.GetLookupAsync(), "Id", "Name", filter.DepartmentId),
            CanEdit = User.IsAdminOrHR()
        };
        return View(model);
    }

    [Authorize(Roles = Roles.AdminOrHR)]
    public async Task<IActionResult> Export([FromQuery] EmployeeFilterDto filter)
    {
        var scope = await _employeeService.GetScopeAsync(User);
        var employees = await _employeeService.GetForExportAsync(filter, scope);
        var bytes = EmployeeExcelExporter.Export(employees);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"employees-{DateTime.Today:yyyy-MM-dd}.xlsx");
    }

    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var employeeId = userId is null ? null : await _employeeService.GetEmployeeIdForUserAsync(userId);
        if (employeeId is null)
        {
            TempData["Error"] = "حسابك مش مربوط بملف موظف";
            return RedirectToAction("Index", "Home");
        }
        return RedirectToAction(nameof(Details), new { id = employeeId });
    }

    public async Task<IActionResult> Details(int id)
    {
        var scope = await _employeeService.GetScopeAsync(User);
        var employee = await _employeeService.GetDetailsAsync(id, scope);
        if (employee is null)
            return NotFound();

        var isAdmin = User.IsInRole(Roles.Admin);
        var model = new EmployeeProfileViewModel
        {
            Employee = employee,
            CanEdit = User.IsAdminOrHR(),
            CanManageAccount = isAdmin,
            CanSeeSalary = User.IsAdminOrHR() || scope.OwnEmployeeId == id,
            Account = isAdmin && employee.UserId is not null ? await _accountService.GetAsync(employee.UserId) : null,
            NewDocument = new UploadDocumentDto { EmployeeId = id },
            Terminate = new TerminateEmployeeDto { EmployeeId = id, TerminationDate = DateOnly.FromDateTime(DateTime.Today) },
            NewAccount = new CreateAccountDto { EmployeeId = id, Email = employee.Email ?? "", Role = Roles.Employee }
        };

        if (model.CanEdit)
        {
            model.Salary = new EmployeeSalaryViewModel
            {
                EmployeeId = id,
                Components = await _componentService.GetForEmployeeAsync(id),
                Loans = await _loanService.GetForEmployeeAsync(id),
                AvailableComponents = new SelectList(await _componentService.GetLookupAsync(), "Id", "Name")
            };
        }
        return View(model);
    }

    public async Task<IActionResult> Photo(int id)
    {
        var scope = await _employeeService.GetScopeAsync(User);
        var path = await _employeeService.GetPhotoPathAsync(id, scope);
        var stream = path is null ? null : _fileStorage.Open(path);
        return stream is null ? NotFound() : File(stream, FileRules.ContentTypeFor(path!));
    }

    [Authorize(Roles = Roles.AdminOrHR)]
    public async Task<IActionResult> Create()
    {
        var model = new EmployeeFormViewModel
        {
            HireDate = DateOnly.FromDateTime(DateTime.Today),
            ContractType = Domain.Enums.ContractType.Permanent
        };
        await FillListsAsync(model);
        return View(model);
    }

    [Authorize(Roles = Roles.AdminOrHR)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EmployeeFormViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _employeeService.CreateAsync(model, model.Photo.ToFileUpload());
            if (result.Succeeded)
            {
                TempData["Success"] = "الموظف اتضاف";
                return RedirectToAction(nameof(Details), new { id = result.Value });
            }
            ModelState.AddServiceError(result);
        }

        await FillListsAsync(model);
        return View(model);
    }

    [Authorize(Roles = Roles.AdminOrHR)]
    public async Task<IActionResult> Edit(int id)
    {
        var dto = await _employeeService.GetForEditAsync(id);
        if (dto is null)
            return NotFound();

        var details = await _employeeService.GetDetailsAsync(id, await _employeeService.GetScopeAsync(User));
        var model = new EmployeeFormViewModel { Code = details?.Code, HasPhoto = details?.HasPhoto ?? false };
        CopyFrom(dto, model);
        await FillListsAsync(model);
        return View(model);
    }

    [Authorize(Roles = Roles.AdminOrHR)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EmployeeFormViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        if (ModelState.IsValid)
        {
            var result = await _employeeService.UpdateAsync(model, model.Photo.ToFileUpload());
            if (result.Succeeded)
            {
                TempData["Success"] = "التعديلات اتحفظت";
                return RedirectToAction(nameof(Details), new { id });
            }
            ModelState.AddServiceError(result);
        }

        await FillListsAsync(model);
        return View(model);
    }

    [Authorize(Roles = Roles.AdminOrHR)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Terminate([Bind(Prefix = "Terminate")] TerminateEmployeeDto dto)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = FirstError();
            return RedirectToAction(nameof(Details), new { id = dto.EmployeeId });
        }

        var result = await _employeeService.TerminateAsync(dto);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "خدمة الموظف انتهت واتقفل حسابه" : result.Error;
        return RedirectToAction(nameof(Details), new { id = dto.EmployeeId });
    }

    private string FirstError()
        => ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault() ?? "البيانات مش صحيحة";

    private async Task FillListsAsync(EmployeeFormViewModel model)
    {
        model.Departments = new SelectList(await _departmentService.GetLookupAsync(), "Id", "Name", model.DepartmentId);
        model.JobTitles = new SelectList(await _jobTitleService.GetLookupAsync(), "Id", "Name", model.JobTitleId);
        model.Managers = new SelectList(await _employeeService.GetManagerLookupAsync(model.Id == 0 ? null : model.Id), "Id", "Name", model.ManagerId);
        model.Shifts = new SelectList(await _employeeService.GetShiftLookupAsync(), "Id", "Name", model.ShiftId);
    }

    private static void CopyFrom(SaveEmployeeDto source, SaveEmployeeDto target)
    {
        foreach (var property in typeof(SaveEmployeeDto).GetProperties())
            property.SetValue(target, property.GetValue(source));
    }
}
