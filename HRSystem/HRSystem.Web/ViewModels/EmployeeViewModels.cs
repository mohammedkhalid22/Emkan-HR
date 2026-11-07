using System.ComponentModel.DataAnnotations;
using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRSystem.Web.ViewModels;

public class EmployeeIndexViewModel
{
    public EmployeeFilterDto Filter { get; set; } = new();
    public PagedResult<EmployeeListItemDto> Result { get; set; } = new();
    public IEnumerable<SelectListItem> Departments { get; set; } = [];
    public bool CanEdit { get; set; }
}

// Flat binding of the DTO plus the dropdowns and the photo upload
public class EmployeeFormViewModel : SaveEmployeeDto
{
    public string? Code { get; set; }
    public bool HasPhoto { get; set; }

    // Set when hiring from recruitment: the candidate's CV and analysis move to the new employee
    public int? CandidateId { get; set; }

    [Display(Name = "الصورة الشخصية")]
    public IFormFile? Photo { get; set; }

    public IEnumerable<SelectListItem> Departments { get; set; } = [];
    public IEnumerable<SelectListItem> JobTitles { get; set; } = [];
    public IEnumerable<SelectListItem> Managers { get; set; } = [];
    public IEnumerable<SelectListItem> Shifts { get; set; } = [];
    public List<PositionOptionDto> Positions { get; set; } = [];
}

public class EmployeeProfileViewModel
{
    public EmployeeDetailsDto Employee { get; set; } = null!;
    public EmployeeAccountDto? Account { get; set; }

    public bool CanEdit { get; set; }
    public bool CanManageAccount { get; set; }
    public bool CanSeeSalary { get; set; }

    public UploadDocumentDto NewDocument { get; set; } = new();
    public TerminateEmployeeDto Terminate { get; set; } = new();
    public CreateAccountDto NewAccount { get; set; } = new();
    public FamilyMemberDto NewFamilyMember { get; set; } = new();
    public QualificationDto NewQualification { get; set; } = new();
    public ExperienceDto NewExperience { get; set; } = new();

    // Filled for HR only
    public EmployeeSalaryViewModel? Salary { get; set; }
    public CvAnalysisPanelDto? Cv { get; set; }
}
