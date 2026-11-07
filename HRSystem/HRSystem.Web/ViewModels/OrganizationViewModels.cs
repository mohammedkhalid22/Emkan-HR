using HRSystem.BLL.DTOs;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRSystem.Web.ViewModels;

// Inherits the DTO so the form binds flat (Name, BranchId, ...) and only adds the dropdown lists
public class DepartmentFormViewModel : SaveDepartmentDto
{
    public IEnumerable<SelectListItem> ParentDepartments { get; set; } = [];
    public IEnumerable<SelectListItem> Branches { get; set; } = [];
    public IEnumerable<SelectListItem> Managers { get; set; } = [];
}

public class PositionFormViewModel : SavePositionDto
{
    public IEnumerable<SelectListItem> Departments { get; set; } = [];
    public IEnumerable<SelectListItem> JobTitles { get; set; } = [];
    public IEnumerable<SelectListItem> Parents { get; set; } = [];
}

public class PositionsIndexViewModel
{
    public List<PositionDto> Positions { get; set; } = [];
    public int? DepartmentId { get; set; }
    public IEnumerable<SelectListItem> Departments { get; set; } = [];
}

public class DashboardViewModel
{
    public string UserName { get; set; } = null!;
    public int BranchesCount { get; set; }
    public int DepartmentsCount { get; set; }
    public int JobTitlesCount { get; set; }
    public int EmployeesCount { get; set; }

    public bool CanApprove { get; set; }
    public int AwaitingCount { get; set; }
    public List<MyBalanceDto> MyBalances { get; set; } = [];
    public TodayAttendanceDto? Today { get; set; }
    public HRSystem.BLL.Services.HrDashboardDto? Hr { get; set; }
}
