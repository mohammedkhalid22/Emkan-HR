using System.ComponentModel.DataAnnotations;
using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRSystem.Web.ViewModels;

public class HolidaysViewModel
{
    public int Year { get; set; }
    public List<PublicHolidayDto> Holidays { get; set; } = [];
    public SavePublicHolidayDto NewHoliday { get; set; } = new();
}

public class LeaveBalancesViewModel
{
    public LeaveBalanceFilterDto Filter { get; set; } = new();
    public List<LeaveBalanceRowDto> Rows { get; set; } = [];
    public IEnumerable<SelectListItem> Departments { get; set; } = [];
    public IEnumerable<SelectListItem> LeaveTypes { get; set; } = [];
}

public class MyLeavesViewModel
{
    public List<MyBalanceDto> Balances { get; set; } = [];
    public List<LeaveRequestDto> Requests { get; set; } = [];
}

public class LeaveRequestFormViewModel : CreateLeaveRequestDto
{
    [Display(Name = "مرفق (تقرير طبي مثلًا)")]
    public IFormFile? Attachment { get; set; }

    public IEnumerable<SelectListItem> LeaveTypes { get; set; } = [];
    public List<MyBalanceDto> Balances { get; set; } = [];
}

public class LeaveRequestsIndexViewModel
{
    public LeaveRequestFilterDto Filter { get; set; } = new();
    public PagedResult<LeaveRequestDto> Result { get; set; } = new();
    public IEnumerable<SelectListItem> Departments { get; set; } = [];
}
