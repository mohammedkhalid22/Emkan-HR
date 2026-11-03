namespace HRSystem.BLL.Common;

// Date rules for the daily reminders (pure, so they can be unit tested)
public static class HrAlerts
{
    public const int ContractWarningDays = 30;
    public const int DocumentWarningDays = 30;

    // Born on 29 February: celebrated on 28 February in years without a 29th
    public static bool IsBirthday(DateOnly birthDate, DateOnly today)
        => birthDate.Month == today.Month && birthDate.Day == today.Day
           || birthDate is { Month: 2, Day: 29 } && today is { Month: 2, Day: 28 } && !DateTime.IsLeapYear(today.Year);

    // Whole years of service completed today, or null when today is not the anniversary (or under a year)
    public static int? AnniversaryYears(DateOnly hireDate, DateOnly today)
    {
        var years = today.Year - hireDate.Year;
        return years >= 1 && IsBirthday(hireDate, today) ? years : null;
    }

    // A date inside the warning window: from today up to `days` ahead
    public static bool IsWithin(DateOnly date, DateOnly today, int days)
        => date >= today && date <= today.AddDays(days);

    public static string MonthName(int month) => month switch
    {
        1 => "يناير", 2 => "فبراير", 3 => "مارس", 4 => "أبريل", 5 => "مايو", 6 => "يونيو",
        7 => "يوليو", 8 => "أغسطس", 9 => "سبتمبر", 10 => "أكتوبر", 11 => "نوفمبر", 12 => "ديسمبر",
        _ => month.ToString()
    };

    // "16 نوفمبر"
    public static string Day(DateOnly date) => $"{date.Day} {MonthName(date.Month)}";
}
