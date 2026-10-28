namespace HRSystem.BLL.Common;

// Pure calculations (no database) so they can be unit tested
public static class LeaveCalculator
{
    // Working days between two dates (inclusive), skipping the weekend and public holidays
    public static int CountWorkingDays(DateOnly start, DateOnly end, IReadOnlySet<DayOfWeek> weekend, IReadOnlySet<DateOnly> holidays)
    {
        if (end < start)
            return 0;

        var count = 0;
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            if (!weekend.Contains(day.DayOfWeek) && !holidays.Contains(day))
                count++;
        }
        return count;
    }

    // 10+ years of service or aged 50+, checked on 1 January of the balance year
    public static bool IsSenior(DateOnly hireDate, DateOnly birthDate, int year)
    {
        var asOf = new DateOnly(year, 1, 1);
        return hireDate.AddYears(10) <= asOf || birthDate.AddYears(50) <= asOf;
    }

    // Yearly entitlement; someone hired during the year gets a share for the months left (hire month counts)
    public static decimal Entitlement(int annualDays, int? seniorAnnualDays, DateOnly hireDate, DateOnly birthDate, int year)
    {
        if (annualDays <= 0 || hireDate.Year > year)
            return 0;

        var days = seniorAnnualDays is int senior && IsSenior(hireDate, birthDate, year) ? senior : annualDays;
        if (hireDate.Year < year)
            return days;

        var monthsLeft = 12 - hireDate.Month + 1;
        var share = days * monthsLeft / 12m;
        return Math.Floor(share * 2) / 2; // round down to the nearest half day
    }

    public static HashSet<DayOfWeek> ParseWeekend(string? setting)
    {
        var days = new HashSet<DayOfWeek>();
        foreach (var part in (setting ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse<DayOfWeek>(part, ignoreCase: true, out var day))
                days.Add(day);
        }
        return days;
    }
}
