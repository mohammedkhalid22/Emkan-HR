using HRSystem.BLL.Common;
using HRSystem.DAL.Repositories;
using HRSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.BLL.Services;

// Which days are working days: the weekend comes from SystemSettings, holidays from PublicHolidays.
// Used by leaves, attendance and later payroll.
public interface IWorkCalendarService
{
    Task<HashSet<DayOfWeek>> GetWeekendAsync();
    Task<HashSet<DateOnly>> GetHolidaysAsync(DateOnly from, DateOnly to);
    Task<bool> IsWorkingDayAsync(DateOnly date);
    Task<int> CountWorkingDaysAsync(DateOnly start, DateOnly end);
}

public class WorkCalendarService : IWorkCalendarService
{
    private const string WeekendKey = "WeekendDays";

    private readonly ISettingsService _settings;
    private readonly IUnitOfWork _unitOfWork;

    public WorkCalendarService(ISettingsService settings, IUnitOfWork unitOfWork)
    {
        _settings = settings;
        _unitOfWork = unitOfWork;
    }

    public async Task<HashSet<DayOfWeek>> GetWeekendAsync()
        => LeaveCalculator.ParseWeekend(await _settings.GetAsync(WeekendKey));

    public async Task<HashSet<DateOnly>> GetHolidaysAsync(DateOnly from, DateOnly to)
        => (await _unitOfWork.Repository<PublicHoliday>().QueryNoTracking()
                .Where(h => h.Date >= from && h.Date <= to)
                .Select(h => h.Date)
                .ToListAsync())
            .ToHashSet();

    public async Task<bool> IsWorkingDayAsync(DateOnly date)
        => !(await GetWeekendAsync()).Contains(date.DayOfWeek) && (await GetHolidaysAsync(date, date)).Count == 0;

    public async Task<int> CountWorkingDaysAsync(DateOnly start, DateOnly end)
        => LeaveCalculator.CountWorkingDays(start, end, await GetWeekendAsync(), await GetHolidaysAsync(start, end));
}
