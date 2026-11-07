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
    private readonly ICvAnalysisService _cvService;
    private readonly IEmployeeDocumentService _documentService;
    private readonly ICandidateService _candidateService;
    private readonly IPositionService _positionService;

    public EmployeesController(
        IEmployeeService employeeService,
        IEmployeeAccountService accountService,
        IDepartmentService departmentService,
        IJobTitleService jobTitleService,
        IFileStorage fileStorage,
        ISalaryComponentService componentService,
        ILoanService loanService,
        ICvAnalysisService cvService,
        IEmployeeDocumentService documentService,
        ICandidateService candidateService,
        IPositionService positionService)
    {
        _positionService = positionService;
        _candidateService = candidateService;
        _cvService = cvService;
        _documentService = documentService;
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
            NewFamilyMember = new FamilyMemberDto { EmployeeId = id },
            NewQualification = new QualificationDto { EmployeeId = id },
            NewExperience = new ExperienceDto { EmployeeId = id },
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
            model.Cv = await _cvService.GetPanelAsync(id);
        }
        return View(model);
    }

    // ---------- AI: CV ----------

    [Authorize(Roles = Roles.AdminOrHR)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AnalyzeCv(int id, int? documentId, IFormFile? file)
    {
        // A new file is saved as the employee's CV first, then analysed
        if (file is not null)
        {
            var upload = await _documentService.UploadAsync(
                new UploadDocumentDto { EmployeeId = id, DocumentType = EmployeeDocumentService.CvDocumentType }, file.ToFileUpload()!);
            if (!upload.Succeeded)
            {
                TempData["Error"] = upload.Error;
                return RedirectToAction(nameof(Details), null, new { id }, "cv");
            }
            documentId = upload.Value;
        }

        var result = documentId is int docId
            ? await _cvService.AnalyzeAsync(id, docId)
            : ServiceResult.Failure("اختار ملف الـ CV أو ارفع واحد جديد");
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "الـ CV اتحلل" : result.Error;
        return RedirectToAction(nameof(Details), null, new { id }, "cv");
    }

    // Reads a CV and returns the new employee form filled with what it found
    [Authorize(Roles = Roles.AdminOrHR)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FillFromCv(IFormFile? cvFile)
    {
        var model = new EmployeeFormViewModel
        {
            HireDate = DateOnly.FromDateTime(DateTime.Today),
            ContractType = Domain.Enums.ContractType.Permanent
        };

        var upload = cvFile.ToFileUpload();
        var result = upload is null
            ? ServiceResult<CvProfile>.Failure("اختار ملف الـ CV الأول")
            : await _cvService.ReadProfileAsync(upload);

        if (result.Succeeded && result.Value is { } cv)
        {
            model.FullNameAr = cv.FullNameAr!;
            model.FullNameEn = cv.FullNameEn;
            model.Email = cv.Email;
            model.Phone = cv.Phone;
            model.Address = cv.Address;
            model.BirthDate = cv.BirthDate;
            model.Gender = cv.Gender;
            model.MaritalStatus = cv.MaritalStatus;

            var found = new[] { cv.FullNameAr ?? cv.FullNameEn, cv.Email, cv.Phone, cv.Address, cv.BirthDate?.ToString(), cv.Gender?.ToString() }
                .Count(v => v is not null);
            ViewData["CvNote"] = found == 0
                ? "ملقيناش بيانات شخصية واضحة في الـ CV ده. كمّل البيانات بإيدك"
                : $"اتملت {found} خانات من الـ CV. راجعها وكمّل الباقي (الرقم القومي والوظيفة والمرتب). مش هيتحفظ حاجة غير لما تدوس حفظ";
        }
        else
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Create));
        }

        // The model is built here, not bound from the form, so don't show validation errors yet
        ModelState.Clear();
        await FillListsAsync(model);
        return View(nameof(Create), model);
    }

    public async Task<IActionResult> Photo(int id)
    {
        var scope = await _employeeService.GetScopeAsync(User);
        var path = await _employeeService.GetPhotoPathAsync(id, scope);
        var stream = path is null ? null : _fileStorage.Open(path);
        return stream is null ? NotFound() : File(stream, FileRules.ContentTypeFor(path!));
    }

    [Authorize(Roles = Roles.AdminOrHR)]
    public async Task<IActionResult> Create(int? candidateId)
    {
        var model = new EmployeeFormViewModel
        {
            HireDate = DateOnly.FromDateTime(DateTime.Today),
            ContractType = Domain.Enums.ContractType.Permanent
        };

        // Hiring from recruitment: start from what the AI read in the CV
        if (candidateId is int id && await _candidateService.GetForHireAsync(id) is { } candidate)
        {
            model.CandidateId = candidate.CandidateId;
            model.JobTitleId = candidate.JobTitleId;
            model.FullNameAr = candidate.FullNameAr!;
            model.FullNameEn = candidate.FullNameEn;
            model.Email = candidate.Email;
            model.Phone = candidate.Phone;
            model.Address = candidate.Address;
            model.BirthDate = candidate.BirthDate;
            model.Gender = candidate.Gender;
            model.MaritalStatus = candidate.MaritalStatus;
            ViewData["CvNote"] = "البيانات جاية من الـ CV بتاع المتقدم. كمّل الرقم القومي والقسم والمرتب واحفظ، والـ CV والتحليل هيتنقلوا لملفه";
        }

        await FillListsAsync(model);
        return View(model);
    }

    [Authorize(Roles = Roles.AdminOrHR)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EmployeeFormViewModel model)
    {
        PositionDecidesJob(model);
        if (ModelState.IsValid)
        {
            var result = await _employeeService.CreateAsync(model, model.Photo.ToFileUpload());
            if (result.Succeeded)
            {
                if (model.CandidateId is int candidateId)
                    await _candidateService.CompleteHireAsync(candidateId, result.Value);
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

        PositionDecidesJob(model);
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

    // With a position picked, the department and job title come from it (the service fills them)
    private void PositionDecidesJob(EmployeeFormViewModel model)
    {
        if (model.PositionId is null)
            return;
        ModelState.Remove(nameof(model.DepartmentId));
        ModelState.Remove(nameof(model.JobTitleId));
    }

    private string FirstError()
        => ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault() ?? "البيانات مش صحيحة";

    private async Task FillListsAsync(EmployeeFormViewModel model)
    {
        model.Departments = new SelectList(await _departmentService.GetLookupAsync(), "Id", "Name", model.DepartmentId);
        model.JobTitles = new SelectList(await _jobTitleService.GetLookupAsync(), "Id", "Name", model.JobTitleId);
        model.Managers = new SelectList(await _employeeService.GetManagerLookupAsync(model.Id == 0 ? null : model.Id), "Id", "Name", model.ManagerId);
        model.Shifts = new SelectList(await _employeeService.GetShiftLookupAsync(), "Id", "Name", model.ShiftId);
        model.Positions = await _positionService.GetOptionsAsync();
    }

    private static void CopyFrom(SaveEmployeeDto source, SaveEmployeeDto target)
    {
        foreach (var property in typeof(SaveEmployeeDto).GetProperties())
            property.SetValue(target, property.GetValue(source));
    }
}
