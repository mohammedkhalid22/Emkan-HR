using HRSystem.BLL.DTOs;
using HRSystem.BLL.Services;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRSystem.Web.ViewModels;

public class LoansViewModel
{
    public bool ShowAll { get; set; }
    public List<LoanDto> Loans { get; set; } = [];
    public IEnumerable<SelectListItem> Employees { get; set; } = [];
    public CreateLoanDto NewLoan { get; set; } = new();
}

public class AdjustmentsViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<AdjustmentDto> Items { get; set; } = [];
    public IEnumerable<SelectListItem> Employees { get; set; } = [];
    public CreateAdjustmentDto NewAdjustment { get; set; } = new();
}

public class SettingsViewModel
{
    public int Year { get; set; }
    public List<SettingDto> Settings { get; set; } = [];
    public List<TaxBracketDto> Brackets { get; set; } = [];
    public SaveTaxBracketDto NewBracket { get; set; } = new();
}

public class UsersViewModel
{
    public List<UserDto> Users { get; set; } = [];
    public string? CurrentUserId { get; set; }
    public CreateUserDto NewUser { get; set; } = new();
}

public class PortalViewModel
{
    public EmployeeDetailsDto Employee { get; set; } = null!;
    public TodayAttendanceDto Today { get; set; } = null!;
    public List<MyBalanceDto> Balances { get; set; } = [];
    public MonthlyAttendanceRowDto? ThisMonth { get; set; }
    public List<LeaveRequestDto> RecentLeaves { get; set; } = [];
    public List<MyPayslipDto> Payslips { get; set; } = [];
    public List<LoanDto> Loans { get; set; } = [];
}

// The salary tab on the employee profile
public class EmployeeSalaryViewModel
{
    public int EmployeeId { get; set; }
    public List<EmployeeComponentDto> Components { get; set; } = [];
    public List<LoanDto> Loans { get; set; } = [];
    public IEnumerable<SelectListItem> AvailableComponents { get; set; } = [];
}
