using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.DAL.Repositories;
using HRSystem.Domain.Entities;
using HRSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.BLL.Services;

public interface IPayrollService
{
    Task<List<PayrollRunDto>> GetRunsAsync();
    Task<PayrollRunDetailsDto?> GetRunAsync(int runId);

    // Creates the month as a draft, or recalculates an existing draft
    Task<ServiceResult<int>> CalculateAsync(int year, int month);
    Task<ServiceResult> ApproveAsync(int runId, string? userId);
    Task<ServiceResult> DeleteDraftAsync(int runId);

    Task<PayslipDto?> GetPayslipAsync(int itemId, ApproverContext viewer);
    Task<List<MyPayslipDto>> GetMyPayslipsAsync(int employeeId);
}

public class PayrollService : IPayrollService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericRepository<PayrollRun> _runs;
    private readonly ISettingsService _settings;
    private readonly IWorkCalendarService _calendar;
    private readonly IAttendanceService _attendance;
    private readonly ILoanService _loans;
    private readonly INotificationService _notifications;

    public PayrollService(IUnitOfWork unitOfWork, ISettingsService settings, IWorkCalendarService calendar,
        IAttendanceService attendance, ILoanService loans, INotificationService notifications)
    {
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _runs = unitOfWork.Repository<PayrollRun>();
        _settings = settings;
        _calendar = calendar;
        _attendance = attendance;
        _loans = loans;
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    // ---------- Queries ----------

    public Task<List<PayrollRunDto>> GetRunsAsync()
        => ProjectRuns(_runs.QueryNoTracking())
            .OrderByDescending(r => r.Year).ThenByDescending(r => r.Month)
            .ToListAsync();

    public async Task<PayrollRunDetailsDto?> GetRunAsync(int runId)
    {
        var run = await ProjectRuns(_runs.QueryNoTracking().Where(r => r.Id == runId)).FirstOrDefaultAsync();
        if (run is null)
            return null;

        var items = await ProjectItems(_unitOfWork.Repository<PayrollItem>().QueryNoTracking().Where(i => i.PayrollRunId == runId))
            .OrderBy(i => i.EmployeeCode)
            .ToListAsync();

        return new PayrollRunDetailsDto { Run = run, Items = items };
    }

    private static IQueryable<PayrollRunDto> ProjectRuns(IQueryable<PayrollRun> query)
        => query.Select(r => new PayrollRunDto
        {
            Id = r.Id,
            Year = r.Year,
            Month = r.Month,
            Status = r.Status,
            EmployeesCount = r.Items.Count,
            TotalGross = r.Items.Sum(i => i.GrossSalary),
            TotalNet = r.Items.Sum(i => i.NetSalary),
            TotalInsuranceCompany = r.Items.Sum(i => i.InsuranceCompany),
            ApprovedAt = r.ApprovedAt
        });

    private static IQueryable<PayrollItemDto> ProjectItems(IQueryable<PayrollItem> query)
        => query.Select(i => new PayrollItemDto
        {
            Id = i.Id,
            EmployeeId = i.EmployeeId,
            EmployeeCode = i.Employee.Code,
            EmployeeName = i.Employee.FullNameAr,
            DepartmentName = i.Employee.Department.Name,
            JobTitleName = i.Employee.JobTitle.Name,
            BankAccount = i.Employee.BankAccount,
            SalaryDays = i.SalaryDays,
            AbsentDays = i.AbsentDays,
            LateMinutes = i.LateMinutes,
            OvertimeMinutes = i.OvertimeMinutes,
            BasicSalary = i.BasicSalary,
            TotalAllowances = i.TotalAllowances,
            OvertimeAmount = i.OvertimeAmount,
            Bonuses = i.Bonuses,
            AbsenceDeduction = i.AbsenceDeduction,
            LateDeduction = i.LateDeduction,
            Penalties = i.Penalties,
            LoanInstallment = i.LoanInstallment,
            OtherDeductions = i.OtherDeductions,
            InsuranceEmployee = i.InsuranceEmployee,
            InsuranceCompany = i.InsuranceCompany,
            IncomeTax = i.IncomeTax,
            GrossSalary = i.GrossSalary,
            NetSalary = i.NetSalary
        });

    // HR sees every payslip; an employee sees their own, once the month is approved
    public async Task<PayslipDto?> GetPayslipAsync(int itemId, ApproverContext viewer)
    {
        var info = await _unitOfWork.Repository<PayrollItem>().QueryNoTracking()
            .Where(i => i.Id == itemId)
            .Select(i => new { i.EmployeeId, i.PayrollRun.Year, i.PayrollRun.Month, i.PayrollRun.Status })
            .FirstOrDefaultAsync();
        if (info is null)
            return null;

        var allowed = viewer.IsHr || (info.EmployeeId == viewer.EmployeeId && info.Status == PayrollRunStatus.Approved);
        if (!allowed)
            return null;

        var item = await ProjectItems(_unitOfWork.Repository<PayrollItem>().QueryNoTracking().Where(i => i.Id == itemId)).FirstAsync();
        return new PayslipDto { Year = info.Year, Month = info.Month, RunStatus = info.Status, Item = item };
    }

    public Task<List<MyPayslipDto>> GetMyPayslipsAsync(int employeeId)
        => _unitOfWork.Repository<PayrollItem>().QueryNoTracking()
            .Where(i => i.EmployeeId == employeeId && i.PayrollRun.Status == PayrollRunStatus.Approved)
            .OrderByDescending(i => i.PayrollRun.Year).ThenByDescending(i => i.PayrollRun.Month)
            .Select(i => new MyPayslipDto
            {
                ItemId = i.Id,
                Year = i.PayrollRun.Year,
                Month = i.PayrollRun.Month,
                GrossSalary = i.GrossSalary,
                NetSalary = i.NetSalary
            })
            .ToListAsync();

    // ---------- Calculate ----------

    public async Task<ServiceResult<int>> CalculateAsync(int year, int month)
    {
        if (month is < 1 or > 12 || year < 2000)
            return ServiceResult<int>.Failure("الشهر مش صحيح");

        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        if (monthStart > Today)
            return ServiceResult<int>.Failure("مينفعش تحسب مرتبات شهر لسه مبدأش");

        var run = await _runs.Query().FirstOrDefaultAsync(r => r.Year == year && r.Month == month);
        if (run?.Status == PayrollRunStatus.Approved)
            return ServiceResult<int>.Failure("مرتبات الشهر ده اتعتمدت ومينفعش تتحسب تاني");

        // Make sure absences / leaves are marked for every past day of the month first
        await _attendance.CloseDaysAsync(monthStart, monthEnd);

        var settings = await LoadSettingsAsync(year);

        if (run is null)
        {
            run = new PayrollRun { Year = year, Month = month, Status = PayrollRunStatus.Draft };
            await _runs.AddAsync(run);
        }
        else
        {
            // Recalculating a draft: start from scratch
            var oldItems = await _unitOfWork.Repository<PayrollItem>().Query().Where(i => i.PayrollRunId == run.Id).ToListAsync();
            oldItems.ForEach(i => _unitOfWork.Repository<PayrollItem>().Remove(i));
        }

        var employees = await _unitOfWork.Repository<Employee>().QueryNoTracking()
            .Where(e => e.HireDate <= monthEnd && (e.TerminationDate == null || e.TerminationDate >= monthStart))
            .Select(e => new { e.Id, e.BasicSalary, e.InsuranceSalary, e.HireDate, e.TerminationDate })
            .ToListAsync();

        foreach (var e in employees)
        {
            var from = e.HireDate > monthStart ? e.HireDate : monthStart;
            var to = e.TerminationDate is DateOnly t && t < monthEnd ? t : monthEnd;
            var fullMonth = from == monthStart && to == monthEnd;
            var salaryDays = fullMonth ? settings.DaysPerMonth : Math.Min(settings.DaysPerMonth, to.DayNumber - from.DayNumber + 1);

            var input = await BuildInputAsync(e.Id, e.BasicSalary, e.InsuranceSalary, salaryDays, monthStart, monthEnd);
            var result = PayrollCalculator.Calculate(input, settings);

            run.Items.Add(new PayrollItem
            {
                EmployeeId = e.Id,
                SalaryDays = salaryDays,
                AbsentDays = input.AbsentDays,
                LateMinutes = input.LateMinutes,
                OvertimeMinutes = input.OvertimeMinutes,
                BasicSalary = result.BasicSalary,
                TotalAllowances = result.TotalAllowances,
                OvertimeAmount = result.OvertimeAmount,
                Bonuses = result.Bonuses,
                AbsenceDeduction = result.AbsenceDeduction,
                LateDeduction = result.LateDeduction,
                Penalties = result.Penalties,
                LoanInstallment = result.LoanInstallment,
                OtherDeductions = result.OtherDeductions,
                InsuranceEmployee = result.InsuranceEmployee,
                InsuranceCompany = result.InsuranceCompany,
                IncomeTax = result.IncomeTax,
                GrossSalary = result.GrossSalary,
                NetSalary = result.NetSalary
            });
        }

        await _unitOfWork.SaveChangesAsync();
        return ServiceResult<int>.Success(run.Id);
    }

    private async Task<PayrollInput> BuildInputAsync(int employeeId, decimal basic, decimal insurance, int salaryDays,
        DateOnly monthStart, DateOnly monthEnd)
    {
        var components = await _unitOfWork.Repository<EmployeeSalaryComponent>().QueryNoTracking()
            .Where(c => c.EmployeeId == employeeId)
            .Select(c => new PayrollComponent(c.SalaryComponent.Type, c.SalaryComponent.CalcMethod, c.Value, c.SalaryComponent.IsTaxable))
            .ToListAsync();

        var attendance = await _unitOfWork.Repository<AttendanceRecord>().QueryNoTracking()
            .Where(r => r.EmployeeId == employeeId && r.Date >= monthStart && r.Date <= monthEnd)
            .Select(r => new { r.Status, r.LateMinutes, r.EarlyLeaveMinutes, r.OvertimeMinutes })
            .ToListAsync();

        // Unpaid leave isn't an absence in attendance, but it isn't paid either
        var unpaidLeaves = await _unitOfWork.Repository<LeaveRequest>().QueryNoTracking()
            .Where(l => l.EmployeeId == employeeId && l.Status == RequestStatus.Approved && !l.LeaveType.IsPaid
                && l.StartDate <= monthEnd && l.EndDate >= monthStart)
            .Select(l => new { l.StartDate, l.EndDate })
            .ToListAsync();
        var unpaidDays = 0;
        foreach (var l in unpaidLeaves)
        {
            unpaidDays += await _calendar.CountWorkingDaysAsync(
                l.StartDate > monthStart ? l.StartDate : monthStart,
                l.EndDate < monthEnd ? l.EndDate : monthEnd);
        }

        var adjustments = await _unitOfWork.Repository<PayrollAdjustment>().QueryNoTracking()
            .Where(a => a.EmployeeId == employeeId && a.Year == monthStart.Year && a.Month == monthStart.Month)
            .ToListAsync();

        return new PayrollInput(
            BasicSalary: basic,
            InsuranceSalary: insurance,
            SalaryDays: salaryDays,
            Components: components,
            AbsentDays: attendance.Count(r => r.Status == AttendanceStatus.Absent) + unpaidDays,
            LateMinutes: attendance.Sum(r => r.LateMinutes + r.EarlyLeaveMinutes),
            OvertimeMinutes: attendance.Sum(r => r.OvertimeMinutes),
            BonusAmount: adjustments.Where(a => a.Type == AdjustmentType.Bonus).Sum(a => a.Amount ?? 0),
            BonusDays: adjustments.Where(a => a.Type == AdjustmentType.Bonus).Sum(a => a.Days ?? 0),
            PenaltyAmount: adjustments.Where(a => a.Type == AdjustmentType.Penalty).Sum(a => a.Amount ?? 0),
            PenaltyDays: adjustments.Where(a => a.Type == AdjustmentType.Penalty).Sum(a => a.Days ?? 0),
            LoanInstallment: await _loans.InstallmentDueAsync(employeeId, monthStart.Year, monthStart.Month));
    }

    private async Task<PayrollSettings> LoadSettingsAsync(int year)
    {
        var bands = await _unitOfWork.Repository<TaxBracket>().QueryNoTracking()
            .Where(b => b.Year == year)
            .Select(b => new TaxBand(b.FromAmount, b.ToAmount, b.Rate))
            .ToListAsync();

        return new PayrollSettings(
            DaysPerMonth: await _settings.GetIntAsync("SalaryDaysPerMonth", 30),
            HoursPerDay: await _settings.GetIntAsync("WorkHoursPerDay", 8),
            OvertimeRate: await _settings.GetDecimalAsync("OvertimeRate", 1.35m),
            InsuranceEmployeeRate: await _settings.GetDecimalAsync("InsuranceEmployeeRate", 0.11m),
            InsuranceCompanyRate: await _settings.GetDecimalAsync("InsuranceCompanyRate", 0.1875m),
            InsuranceMinSalary: await _settings.GetDecimalAsync("InsuranceMinSalary", 0),
            InsuranceMaxSalary: await _settings.GetDecimalAsync("InsuranceMaxSalary", 0),
            AnnualTaxExemption: await _settings.GetDecimalAsync("TaxPersonalExemption", 0),
            TaxBands: bands);
    }

    // ---------- Approve / delete ----------

    public async Task<ServiceResult> ApproveAsync(int runId, string? userId)
    {
        var run = await _runs.Query().Include(r => r.Items).FirstOrDefaultAsync(r => r.Id == runId);
        if (run is null)
            return ServiceResult.Failure("المسير مش موجود");
        if (run.Status == PayrollRunStatus.Approved)
            return ServiceResult.Failure("المسير معتمد بالفعل");
        if (run.Items.Count == 0)
            return ServiceResult.Failure("المسير فاضي");

        // Loan installments are only paid off once the month is final
        foreach (var item in run.Items.Where(i => i.LoanInstallment > 0))
            await _loans.PayInstallmentAsync(item.EmployeeId, run.Year, run.Month, item.LoanInstallment);

        run.Status = PayrollRunStatus.Approved;
        run.ApprovedBy = userId;
        run.ApprovedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();
        await _notifications.PayslipsReadyAsync(run.Id);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> DeleteDraftAsync(int runId)
    {
        var run = await _runs.Query().Include(r => r.Items).FirstOrDefaultAsync(r => r.Id == runId);
        if (run is null)
            return ServiceResult.Failure("المسير مش موجود");
        if (run.Status == PayrollRunStatus.Approved)
            return ServiceResult.Failure("مينفعش تمسح مسير معتمد");

        foreach (var item in run.Items.ToList())
            _unitOfWork.Repository<PayrollItem>().Remove(item);
        _runs.Remove(run);

        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }
}
