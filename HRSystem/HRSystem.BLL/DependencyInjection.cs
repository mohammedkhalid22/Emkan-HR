using HRSystem.BLL.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HRSystem.BLL;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessLogic(this IServiceCollection services)
    {
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IJobTitleService, JobTitleService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IEmployeeDocumentService, EmployeeDocumentService>();
        services.AddScoped<IEmployeeAccountService, EmployeeAccountService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IWorkCalendarService, WorkCalendarService>();
        services.AddScoped<IShiftService, ShiftService>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddScoped<IAttendanceRequestService, AttendanceRequestService>();
        services.AddScoped<ISalaryComponentService, SalaryComponentService>();
        services.AddScoped<ILoanService, LoanService>();
        services.AddScoped<IPayrollAdjustmentService, PayrollAdjustmentService>();
        services.AddScoped<ITaxBracketService, TaxBracketService>();
        services.AddScoped<IPayrollService, PayrollService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ILeaveTypeService, LeaveTypeService>();
        services.AddScoped<IPublicHolidayService, PublicHolidayService>();
        services.AddScoped<ILeaveBalanceService, LeaveBalanceService>();
        services.AddScoped<ILeaveRequestService, LeaveRequestService>();
        return services;
    }
}
