using HRSystem.BLL.Common;
using HRSystem.DAL.Repositories;
using HRSystem.Domain.Entities;
using HRSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.BLL.Services;

public interface IAskService
{
    bool IsAvailable { get; }

    // Answers a question about one month of HR data (plus the current employee list, balances and loans)
    Task<ServiceResult<AskAnswer>> AskAsync(string question, int year, int month);
}

public class AskService : IAskService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISettingsService _settings;
    private readonly IAiClient _ai;

    public AskService(IUnitOfWork unitOfWork, ISettingsService settings, IAiClient ai)
    {
        _unitOfWork = unitOfWork;
        _settings = settings;
        _ai = ai;
    }

    public bool IsAvailable => _ai.IsConfigured;

    public async Task<ServiceResult<AskAnswer>> AskAsync(string question, int year, int month)
    {
        if (!_ai.IsConfigured)
            return ServiceResult<AskAnswer>.Failure(CvAnalysisService.NotConfigured);

        question = question.Trim();
        if (question.Length == 0)
            return ServiceResult<AskAnswer>.Failure("اكتب سؤالك الأول");
        if (question.Length > HrAsk.MaxQuestionLength)
            return ServiceResult<AskAnswer>.Failure($"السؤال طويل. خليه أقل من {HrAsk.MaxQuestionLength} حرف");

        var data = await BuildDataAsync(year, month);
        var prompt = $"""
            {data}
            ---
            Question from the HR team (answer it from the tables above):
            {question}
            """;

        var reply = await _ai.GenerateJsonAsync(HrAsk.Instructions, prompt, null);
        if (!reply.Succeeded)
            return ServiceResult<AskAnswer>.From(reply);

        return HrAsk.ParseAnswer(reply.Value!) is { } answer
            ? ServiceResult<AskAnswer>.Success(answer)
            : ServiceResult<AskAnswer>.Failure("الذكاء الاصطناعي رجّع رد مش مفهوم. جرّب تصيغ السؤال بشكل تاني");
    }

    // Everything the AI may use, as small tab-separated tables
    private async Task<string> BuildDataAsync(int year, int month)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        var today = DateOnly.FromDateTime(DateTime.Today);

        var employees = await _unitOfWork.Repository<Employee>().QueryNoTracking()
            .OrderBy(e => e.Code)
            .Select(e => new
            {
                e.Id, e.Code, e.FullNameAr, Department = e.Department.Name, Branch = e.Department.Branch!.Name,
                Job = e.JobTitle.Name, Manager = e.Manager!.FullNameAr, e.Status, e.HireDate, e.TerminationDate,
                e.BirthDate, e.Gender, e.ContractType, e.ContractEndDate, e.BasicSalary, e.AllowRemoteAttendance, Shift = e.Shift!.Name
            })
            .ToListAsync();
        var codes = employees.ToDictionary(e => e.Id, e => e.Code);

        var records = await _unitOfWork.Repository<AttendanceRecord>().QueryNoTracking()
            .Where(r => r.Date >= from && r.Date <= to)
            .Select(r => new { r.EmployeeId, r.Date, r.Status, r.CheckIn, r.CheckOut, r.LateMinutes, r.EarlyLeaveMinutes, r.OvertimeMinutes, r.CheckInDistanceMeters })
            .ToListAsync();

        var leaves = await _unitOfWork.Repository<LeaveRequest>().QueryNoTracking()
            .Where(l => l.StartDate <= to && l.EndDate >= new DateOnly(year, 1, 1))
            .OrderBy(l => l.StartDate)
            .Select(l => new { l.EmployeeId, Type = l.LeaveType.Name, l.StartDate, l.EndDate, l.Days, l.Status })
            .ToListAsync();

        var balances = await _unitOfWork.Repository<LeaveBalance>().QueryNoTracking()
            .Where(b => b.Year == year)
            .Select(b => new { b.EmployeeId, Type = b.LeaveType.Name, b.Entitled, b.Used })
            .ToListAsync();

        var permissions = await _unitOfWork.Repository<AttendanceRequest>().QueryNoTracking()
            .Where(p => p.Date >= from && p.Date <= to)
            .Select(p => new { p.EmployeeId, p.Type, p.Date, p.FromTime, p.ToTime, p.Status })
            .ToListAsync();

        var loans = await _unitOfWork.Repository<Loan>().QueryNoTracking()
            .Where(l => l.Status == LoanStatus.Active)
            .Select(l => new { l.EmployeeId, l.Amount, l.MonthlyInstallment, l.RemainingAmount, l.StartMonth })
            .ToListAsync();

        var candidates = await _unitOfWork.Repository<Candidate>().QueryNoTracking()
            .OrderByDescending(c => c.MatchPercent)
            .Select(c => new { Name = c.FullNameAr ?? c.FullNameEn, Job = c.JobTitle.Name, c.MatchPercent, c.ExperienceYears, c.Status, c.CreatedAt })
            .ToListAsync();

        string Code(int id) => codes.TryGetValue(id, out var code) ? code : id.ToString();
        int Age(DateOnly birth) => today.Year - birth.Year - (today < birth.AddYears(today.Year - birth.Year) ? 1 : 0);

        var parts = new List<string>
        {
            $"""
            Today: {HrAsk.Cell(today)}. Attendance period: {HrAsk.Cell(from)} to {HrAsk.Cell(to)}.
            Weekend days: {await _settings.GetAsync(SettingsService.WeekendDays) ?? "Friday,Saturday"}.
            Minutes are whole minutes. Employees are joined across tables by Code. Money is in Egyptian pounds.

            """,

            HrAsk.Table("Employees",
                ["Code", "Name", "Department", "Branch", "JobTitle", "DirectManager", "Status", "HireDate", "YearsOfService",
                 "TerminationDate", "Age", "Gender", "Contract", "ContractEnd", "BasicSalary", "MayCheckInAnywhere", "Shift"],
                employees.Select(e => new object?[]
                {
                    e.Code, e.FullNameAr, e.Department, e.Branch, e.Job, e.Manager, e.Status, e.HireDate,
                    Math.Round((today.DayNumber - e.HireDate.DayNumber) / 365.25, 1), e.TerminationDate, Age(e.BirthDate),
                    e.Gender, e.ContractType, e.ContractEndDate, e.BasicSalary, e.AllowRemoteAttendance, e.Shift
                })),

            // Totals per employee so the AI never has to add up days itself
            HrAsk.Table($"Attendance totals per employee for {from:yyyy-MM}",
                ["Code", "DaysPresent", "DaysAbsent", "DaysOnLeave", "DaysMission", "TimesLate", "TotalLateMinutes",
                 "TimesLeftEarly", "TotalEarlyLeaveMinutes", "TotalOvertimeMinutes", "AverageCheckIn"],
                records.GroupBy(r => r.EmployeeId).OrderBy(g => Code(g.Key)).Select(g =>
                {
                    var checkIns = g.Where(r => r.CheckIn is not null).Select(r => r.CheckIn!.Value.ToTimeSpan().TotalMinutes).ToList();
                    return new object?[]
                    {
                        Code(g.Key),
                        g.Count(r => r.Status == AttendanceStatus.Present),
                        g.Count(r => r.Status == AttendanceStatus.Absent),
                        g.Count(r => r.Status == AttendanceStatus.OnLeave),
                        g.Count(r => r.Status == AttendanceStatus.Mission),
                        g.Count(r => r.LateMinutes > 0),
                        g.Sum(r => r.LateMinutes),
                        g.Count(r => r.EarlyLeaveMinutes > 0),
                        g.Sum(r => r.EarlyLeaveMinutes),
                        g.Sum(r => r.OvertimeMinutes),
                        checkIns.Count == 0 ? null : TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(checkIns.Average()))
                    };
                })),

            HrAsk.Table($"Attendance by day for {from:yyyy-MM}",
                ["Date", "Code", "Status", "CheckIn", "CheckOut", "LateMinutes", "EarlyLeaveMinutes", "OvertimeMinutes", "CheckInMetersFromBranch"],
                records.OrderBy(r => r.Date).ThenBy(r => Code(r.EmployeeId)).Select(r => new object?[]
                {
                    r.Date, Code(r.EmployeeId), r.Status, r.CheckIn, r.CheckOut, r.LateMinutes, r.EarlyLeaveMinutes, r.OvertimeMinutes, r.CheckInDistanceMeters
                })),

            HrAsk.Table($"Leave requests in {year}",
                ["Code", "LeaveType", "From", "To", "Days", "Status"],
                leaves.Select(l => new object?[] { Code(l.EmployeeId), l.Type, l.StartDate, l.EndDate, l.Days, l.Status })),

            HrAsk.Table($"Leave balances {year}",
                ["Code", "LeaveType", "Entitled", "Used", "Remaining"],
                balances.OrderBy(b => Code(b.EmployeeId)).Select(b => new object?[] { Code(b.EmployeeId), b.Type, b.Entitled, b.Used, b.Entitled - b.Used })),

            HrAsk.Table($"Permissions and missions in {from:yyyy-MM}",
                ["Code", "Type", "Date", "From", "To", "Status"],
                permissions.OrderBy(p => p.Date).Select(p => new object?[] { Code(p.EmployeeId), p.Type, p.Date, p.FromTime, p.ToTime, p.Status })),

            HrAsk.Table("Open loans",
                ["Code", "Amount", "MonthlyInstallment", "Remaining", "StartMonth"],
                loans.Select(l => new object?[] { Code(l.EmployeeId), l.Amount, l.MonthlyInstallment, l.RemainingAmount, l.StartMonth })),

            HrAsk.Table("Job applicants (recruitment)",
                ["Name", "JobTitle", "AiMatchPercent", "ExperienceYears", "Status", "AppliedOn"],
                candidates.Select(c => new object?[] { c.Name, c.Job, c.MatchPercent, c.ExperienceYears, c.Status, DateOnly.FromDateTime(c.CreatedAt) }))
        };

        return string.Concat(parts);
    }
}
