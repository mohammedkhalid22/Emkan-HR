using HRSystem.DAL.Repositories;
using HRSystem.Domain.Entities;
using HRSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.BLL.Services;

public record ChartPoint(string Label, decimal Value);

public record ChartSeries(string Name, List<decimal> Values);

public record LateEmployee(int EmployeeId, string Name, string Department, int LateMinutes, int LateDays);

// Everything the statistics page shows, for one year
public class StatisticsDto
{
    public int Year { get; set; }
    public List<int> Years { get; set; } = [];
    public bool IsCurrentYear { get; set; }

    // ---------- Headline numbers ----------
    public int ActiveEmployees { get; set; }
    public int Hires { get; set; }
    public int Terminations { get; set; }
    public decimal TurnoverRate { get; set; }           // % of the average headcount
    public decimal? AttendanceRate { get; set; }         // present ÷ (present + absent), %
    public decimal ApprovedLeaveDays { get; set; }
    public int PendingRequests { get; set; }
    public decimal PayrollCost { get; set; }             // gross + company insurance, all runs of the year
    public decimal AverageBasicSalary { get; set; }
    public decimal LoansOutstanding { get; set; }
    public int ActiveLoans { get; set; }

    // ---------- Workforce ----------
    public List<ChartPoint> ByDepartment { get; set; } = [];
    public List<ChartPoint> ByGender { get; set; } = [];
    public List<ChartPoint> ByContract { get; set; } = [];
    public List<ChartPoint> ByAge { get; set; } = [];
    public List<ChartPoint> ByTenure { get; set; } = [];
    public List<ChartSeries> HiresVsTerminations { get; set; } = [];   // per month

    // ---------- Attendance ----------
    public List<decimal?> MonthlyAttendanceRate { get; set; } = [];   // % per month, null = no data
    public List<decimal> MonthlyLateHours { get; set; } = [];
    public List<decimal> MonthlyOvertimeHours { get; set; } = [];
    public List<string> DailyLabels { get; set; } = [];
    public List<ChartSeries> DailyStatus { get; set; } = [];           // last 30 days, stacked
    public List<LateEmployee> TopLate { get; set; } = [];

    // ---------- Leaves ----------
    public List<ChartPoint> LeaveDaysByType { get; set; } = [];
    public List<decimal> MonthlyLeaveDays { get; set; } = [];
    public List<ChartPoint> RequestsByStatus { get; set; } = [];

    // ---------- Payroll ----------
    public List<decimal?> MonthlyNet { get; set; } = [];               // null = month not run
    public List<decimal?> MonthlyCost { get; set; } = [];
    public string? LatestRunLabel { get; set; }
    public List<ChartPoint> LatestEarnings { get; set; } = [];
    public List<ChartPoint> LatestDeductions { get; set; } = [];
    public List<ChartPoint> AverageNetByDepartment { get; set; } = [];
}

public interface IStatisticsService
{
    Task<StatisticsDto> GetAsync(int year);
}

public class StatisticsService : IStatisticsService
{
    private readonly IUnitOfWork _unitOfWork;

    public StatisticsService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    public async Task<StatisticsDto> GetAsync(int year)
    {
        var yearStart = new DateOnly(year, 1, 1);
        var yearEnd = new DateOnly(year, 12, 31);
        var asOf = yearEnd < Today ? yearEnd : Today;   // "now" for a past year is its last day

        var dto = new StatisticsDto { Year = year, IsCurrentYear = year == Today.Year };

        await FillWorkforceAsync(dto, yearStart, yearEnd, asOf);
        await FillAttendanceAsync(dto, yearStart, yearEnd);
        await FillLeavesAsync(dto, yearStart, yearEnd);
        await FillPayrollAsync(dto);
        return dto;
    }

    // ---------- Workforce ----------

    private async Task FillWorkforceAsync(StatisticsDto dto, DateOnly yearStart, DateOnly yearEnd, DateOnly asOf)
    {
        var all = await _unitOfWork.Repository<Employee>().QueryNoTracking()
            .Select(e => new
            {
                e.Gender, e.BirthDate, e.HireDate, e.TerminationDate, e.ContractType, e.BasicSalary,
                Department = e.Department.Name
            })
            .ToListAsync();

        bool EmployedOn(DateOnly hire, DateOnly? end, DateOnly day) => hire <= day && (end == null || end >= day);

        var current = all.Where(e => EmployedOn(e.HireDate, e.TerminationDate, asOf)).ToList();
        var firstHireYear = all.Count == 0 ? Today.Year : all.Min(e => e.HireDate.Year);
        dto.Years = Enumerable.Range(firstHireYear, Today.Year - firstHireYear + 1).Reverse().ToList();

        dto.ActiveEmployees = current.Count;
        dto.Hires = all.Count(e => e.HireDate.Year == dto.Year);
        dto.Terminations = all.Count(e => e.TerminationDate?.Year == dto.Year);
        dto.AverageBasicSalary = current.Count == 0 ? 0 : Math.Round(current.Average(e => e.BasicSalary), 0);

        // Turnover = leavers ÷ average of headcount at the start and at the end of the period
        var startCount = all.Count(e => EmployedOn(e.HireDate, e.TerminationDate, yearStart));
        var average = (startCount + current.Count) / 2m;
        dto.TurnoverRate = average == 0 ? 0 : Math.Round(dto.Terminations / average * 100, 1);

        dto.ByDepartment = current.GroupBy(e => e.Department)
            .Select(g => new ChartPoint(g.Key, g.Count()))
            .OrderByDescending(p => p.Value).ToList();

        dto.ByGender =
        [
            new("ذكور", current.Count(e => e.Gender == Gender.Male)),
            new("إناث", current.Count(e => e.Gender == Gender.Female))
        ];

        dto.ByContract =
        [
            new("دائم", current.Count(e => e.ContractType == ContractType.Permanent)),
            new("مؤقت", current.Count(e => e.ContractType == ContractType.Temporary)),
            new("دوام جزئي", current.Count(e => e.ContractType == ContractType.PartTime))
        ];

        int Years(DateOnly from) => asOf.Year - from.Year - (asOf.DayOfYear < from.DayOfYear ? 1 : 0);

        var ages = current.Select(e => Years(e.BirthDate)).ToList();
        dto.ByAge =
        [
            new("أقل من 25", ages.Count(a => a < 25)),
            new("25 إلى 34", ages.Count(a => a is >= 25 and < 35)),
            new("35 إلى 44", ages.Count(a => a is >= 35 and < 45)),
            new("45 إلى 54", ages.Count(a => a is >= 45 and < 55)),
            new("55 فأكثر", ages.Count(a => a >= 55))
        ];

        var tenure = current.Select(e => Years(e.HireDate)).ToList();
        dto.ByTenure =
        [
            new("أقل من سنة", tenure.Count(t => t < 1)),
            new("1 إلى 2", tenure.Count(t => t is >= 1 and < 3)),
            new("3 إلى 4", tenure.Count(t => t is >= 3 and < 5)),
            new("5 إلى 9", tenure.Count(t => t is >= 5 and < 10)),
            new("10 فأكثر", tenure.Count(t => t >= 10))
        ];

        dto.HiresVsTerminations =
        [
            new("تعيينات", Months(m => all.Count(e => e.HireDate.Year == dto.Year && e.HireDate.Month == m))),
            new("إنهاء خدمة", Months(m => all.Count(e => e.TerminationDate?.Year == dto.Year && e.TerminationDate?.Month == m)))
        ];

        var loans = await _unitOfWork.Repository<Loan>().QueryNoTracking()
            .Where(l => l.Status == LoanStatus.Active)
            .Select(l => l.RemainingAmount)
            .ToListAsync();
        dto.LoansOutstanding = loans.Sum();
        dto.ActiveLoans = loans.Count;
    }

    // ---------- Attendance ----------

    private async Task FillAttendanceAsync(StatisticsDto dto, DateOnly yearStart, DateOnly yearEnd)
    {
        var records = await _unitOfWork.Repository<AttendanceRecord>().QueryNoTracking()
            .Where(r => r.Date >= yearStart && r.Date <= yearEnd)
            .Select(r => new
            {
                r.EmployeeId, r.Date, r.Status, Late = r.LateMinutes + r.EarlyLeaveMinutes, r.OvertimeMinutes,
                Name = r.Employee.FullNameAr, Department = r.Employee.Department.Name
            })
            .ToListAsync();

        decimal? Rate(IEnumerable<AttendanceStatus> statuses)
        {
            var list = statuses.ToList();
            var present = list.Count(s => s == AttendanceStatus.Present);
            var absent = list.Count(s => s == AttendanceStatus.Absent);
            return present + absent == 0 ? null : Math.Round(present * 100m / (present + absent), 1);
        }

        dto.AttendanceRate = Rate(records.Select(r => r.Status));
        dto.MonthlyAttendanceRate = Enumerable.Range(1, 12)
            .Select(m => Rate(records.Where(r => r.Date.Month == m).Select(r => r.Status)))
            .ToList();
        dto.MonthlyLateHours = Months(m => Math.Round(records.Where(r => r.Date.Month == m).Sum(r => r.Late) / 60m, 1));
        dto.MonthlyOvertimeHours = Months(m => Math.Round(records.Where(r => r.Date.Month == m).Sum(r => r.OvertimeMinutes) / 60m, 1));

        dto.TopLate = records.Where(r => r.Late > 0)
            .GroupBy(r => new { r.EmployeeId, r.Name, r.Department })
            .Select(g => new LateEmployee(g.Key.EmployeeId, g.Key.Name, g.Key.Department, g.Sum(r => r.Late), g.Count()))
            .OrderByDescending(x => x.LateMinutes)
            .Take(5)
            .ToList();

        // Last 30 days up to today (or the year's last 30 days for a past year)
        var end = yearEnd < Today ? yearEnd : Today;
        var days = Enumerable.Range(0, 30).Select(i => end.AddDays(i - 29)).ToList();
        var recent = await _unitOfWork.Repository<AttendanceRecord>().QueryNoTracking()
            .Where(r => r.Date >= days[0] && r.Date <= end)
            .Select(r => new { r.Date, r.Status })
            .ToListAsync();

        dto.DailyLabels = days.Select(d => d.ToString("dd/MM")).ToList();
        dto.DailyStatus =
        [
            new("حضور", days.Select(d => (decimal)recent.Count(r => r.Date == d && r.Status == AttendanceStatus.Present)).ToList()),
            new("إجازة", days.Select(d => (decimal)recent.Count(r => r.Date == d && r.Status == AttendanceStatus.OnLeave)).ToList()),
            new("مأمورية", days.Select(d => (decimal)recent.Count(r => r.Date == d && r.Status == AttendanceStatus.Mission)).ToList()),
            new("غياب", days.Select(d => (decimal)recent.Count(r => r.Date == d && r.Status == AttendanceStatus.Absent)).ToList())
        ];
    }

    // ---------- Leaves ----------

    private async Task FillLeavesAsync(StatisticsDto dto, DateOnly yearStart, DateOnly yearEnd)
    {
        var requests = await _unitOfWork.Repository<LeaveRequest>().QueryNoTracking()
            .Where(r => r.StartDate >= yearStart && r.StartDate <= yearEnd)
            .Select(r => new { r.Status, r.Days, r.StartDate, Type = r.LeaveType.Name })
            .ToListAsync();

        var approved = requests.Where(r => r.Status == RequestStatus.Approved).ToList();
        dto.ApprovedLeaveDays = approved.Sum(r => r.Days);
        dto.LeaveDaysByType = approved.GroupBy(r => r.Type)
            .Select(g => new ChartPoint(g.Key, g.Sum(r => r.Days)))
            .OrderByDescending(p => p.Value).ToList();
        dto.MonthlyLeaveDays = Months(m => approved.Where(r => r.StartDate.Month == m).Sum(r => r.Days));

        dto.RequestsByStatus =
        [
            new("معتمدة", requests.Count(r => r.Status == RequestStatus.Approved)),
            new("قيد الموافقة", requests.Count(r => r.Status is RequestStatus.Pending or RequestStatus.ManagerApproved)),
            new("مرفوضة", requests.Count(r => r.Status == RequestStatus.Rejected)),
            new("ملغية", requests.Count(r => r.Status == RequestStatus.Cancelled))
        ];

        // Waiting right now, any year (leave + permission / mission requests)
        dto.PendingRequests =
            await _unitOfWork.Repository<LeaveRequest>().QueryNoTracking()
                .CountAsync(r => r.Status == RequestStatus.Pending || r.Status == RequestStatus.ManagerApproved)
            + await _unitOfWork.Repository<AttendanceRequest>().QueryNoTracking()
                .CountAsync(r => r.Status == RequestStatus.Pending || r.Status == RequestStatus.ManagerApproved);
    }

    // ---------- Payroll ----------

    private async Task FillPayrollAsync(StatisticsDto dto)
    {
        var runs = await _unitOfWork.Repository<PayrollRun>().QueryNoTracking()
            .Where(r => r.Year == dto.Year)
            .Select(r => new
            {
                r.Month,
                Net = r.Items.Sum(i => i.NetSalary),
                Cost = r.Items.Sum(i => i.GrossSalary + i.InsuranceCompany)
            })
            .ToListAsync();

        dto.PayrollCost = runs.Sum(r => r.Cost);
        dto.MonthlyNet = Enumerable.Range(1, 12).Select(m => runs.FirstOrDefault(r => r.Month == m)?.Net).ToList();
        dto.MonthlyCost = Enumerable.Range(1, 12).Select(m => runs.FirstOrDefault(r => r.Month == m)?.Cost).ToList();

        // Breakdown of the most recent run in the year
        var latest = await _unitOfWork.Repository<PayrollRun>().QueryNoTracking()
            .Where(r => r.Year == dto.Year)
            .OrderByDescending(r => r.Month)
            .Select(r => new { r.Id, r.Month, r.Status })
            .FirstOrDefaultAsync();
        if (latest is null)
            return;

        dto.LatestRunLabel = $"{latest.Month:D2}/{dto.Year}" + (latest.Status == PayrollRunStatus.Draft ? " (مسودة)" : "");

        var items = await _unitOfWork.Repository<PayrollItem>().QueryNoTracking()
            .Where(i => i.PayrollRunId == latest.Id)
            .Select(i => new
            {
                i.BasicSalary, i.TotalAllowances, i.OvertimeAmount, i.Bonuses,
                i.InsuranceEmployee, i.IncomeTax, Attendance = i.AbsenceDeduction + i.LateDeduction,
                i.Penalties, i.LoanInstallment, i.OtherDeductions, i.NetSalary,
                Department = i.Employee.Department.Name
            })
            .ToListAsync();

        dto.LatestEarnings =
        [
            new("المرتب الأساسي", items.Sum(i => i.BasicSalary)),
            new("البدلات", items.Sum(i => i.TotalAllowances)),
            new("الأوفر تايم", items.Sum(i => i.OvertimeAmount)),
            new("المكافآت", items.Sum(i => i.Bonuses))
        ];
        dto.LatestDeductions =
        [
            new("الغياب والتأخير", items.Sum(i => i.Attendance)),
            new("التأمينات", items.Sum(i => i.InsuranceEmployee)),
            new("الضريبة", items.Sum(i => i.IncomeTax)),
            new("السلف", items.Sum(i => i.LoanInstallment)),
            new("الجزاءات", items.Sum(i => i.Penalties)),
            new("أخرى", items.Sum(i => i.OtherDeductions))
        ];
        dto.AverageNetByDepartment = items.GroupBy(i => i.Department)
            .Select(g => new ChartPoint(g.Key, Math.Round(g.Average(i => i.NetSalary), 0)))
            .OrderByDescending(p => p.Value).ToList();
    }

    private static List<decimal> Months(Func<int, decimal> valueForMonth)
        => Enumerable.Range(1, 12).Select(valueForMonth).ToList();
}
