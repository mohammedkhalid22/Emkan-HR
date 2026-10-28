using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRSystem.Web.ViewModels;

public class AttendanceIndexViewModel
{
    public AttendanceFilterDto Filter { get; set; } = new();
    public PagedResult<AttendanceRecordDto> Result { get; set; } = new();
    public IEnumerable<SelectListItem> Departments { get; set; } = [];
    public bool ShowEmployee { get; set; }
    public bool CanEdit { get; set; }
    public TodayAttendanceDto? Today { get; set; }
}

public class MonthlyAttendanceViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int? DepartmentId { get; set; }
    public List<MonthlyAttendanceRowDto> Rows { get; set; } = [];
    public IEnumerable<SelectListItem> Departments { get; set; } = [];
}

public class AttendanceImportViewModel
{
    public string? Token { get; set; }
    public string? FileName { get; set; }
    public List<AttendanceImportResultDto> Results { get; set; } = [];
}

public class MyAttendanceRequestsViewModel
{
    public List<AttendanceRequestDto> Requests { get; set; } = [];
    public CreateAttendanceRequestDto NewRequest { get; set; } = new();
}

public class AwaitingViewModel
{
    public List<LeaveRequestDto> Leaves { get; set; } = [];
    public List<AttendanceRequestDto> AttendanceRequests { get; set; } = [];
}
