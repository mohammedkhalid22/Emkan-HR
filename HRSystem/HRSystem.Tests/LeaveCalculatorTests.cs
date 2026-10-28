using HRSystem.BLL.Common;

namespace HRSystem.Tests;

public class LeaveCalculatorTests
{
    private static readonly HashSet<DayOfWeek> FridaySaturday = [DayOfWeek.Friday, DayOfWeek.Saturday];
    private static readonly HashSet<DateOnly> NoHolidays = [];

    // ---------- Working days ----------

    [Fact]
    public void Thursday_to_Sunday_skips_the_weekend()
    {
        // 2026-10-29 is a Thursday, 2026-11-01 a Sunday
        var days = LeaveCalculator.CountWorkingDays(new(2026, 10, 29), new(2026, 11, 1), FridaySaturday, NoHolidays);
        Assert.Equal(2, days);
    }

    [Fact]
    public void Full_week_has_five_working_days()
    {
        var days = LeaveCalculator.CountWorkingDays(new(2026, 11, 1), new(2026, 11, 7), FridaySaturday, NoHolidays);
        Assert.Equal(5, days);
    }

    [Fact]
    public void Public_holiday_is_not_counted()
    {
        HashSet<DateOnly> holidays = [new(2026, 11, 2)];
        var days = LeaveCalculator.CountWorkingDays(new(2026, 11, 1), new(2026, 11, 3), FridaySaturday, holidays);
        Assert.Equal(2, days);
    }

    [Fact]
    public void Weekend_only_period_is_zero_days()
    {
        var days = LeaveCalculator.CountWorkingDays(new(2026, 10, 30), new(2026, 10, 31), FridaySaturday, NoHolidays);
        Assert.Equal(0, days);
    }

    [Fact]
    public void End_before_start_is_zero_days()
    {
        var days = LeaveCalculator.CountWorkingDays(new(2026, 11, 5), new(2026, 11, 1), FridaySaturday, NoHolidays);
        Assert.Equal(0, days);
    }

    [Fact]
    public void Weekend_setting_is_parsed()
    {
        var weekend = LeaveCalculator.ParseWeekend("Friday, Saturday, nonsense");
        Assert.Equal(FridaySaturday, weekend);
    }

    // ---------- Entitlement ----------

    private static readonly DateOnly Born1990 = new(1990, 5, 15);

    [Fact]
    public void Full_year_gets_the_normal_days()
        => Assert.Equal(21m, LeaveCalculator.Entitlement(21, 30, new(2020, 3, 1), Born1990, 2026));

    [Fact]
    public void Ten_years_of_service_gets_senior_days()
        => Assert.Equal(30m, LeaveCalculator.Entitlement(21, 30, new(2016, 1, 1), Born1990, 2026));

    [Fact]
    public void Nine_years_and_a_bit_is_not_senior_yet()
        => Assert.Equal(21m, LeaveCalculator.Entitlement(21, 30, new(2016, 1, 2), Born1990, 2026));

    [Fact]
    public void Age_fifty_gets_senior_days()
        => Assert.Equal(30m, LeaveCalculator.Entitlement(21, 30, new(2024, 1, 1), new(1975, 6, 1), 2026));

    [Fact]
    public void No_senior_days_configured_keeps_normal_days()
        => Assert.Equal(7m, LeaveCalculator.Entitlement(7, null, new(2010, 1, 1), Born1990, 2026));

    [Theory]
    [InlineData(1, 21)]     // hired in January: full year
    [InlineData(3, 17.5)]   // 10 months: 17.5
    [InlineData(7, 10.5)]   // 6 months: 10.5
    [InlineData(12, 1.5)]   // 1 month: 1.75 rounded down to 1.5
    public void Hired_during_the_year_gets_a_share(int hireMonth, decimal expected)
        => Assert.Equal(expected, LeaveCalculator.Entitlement(21, 30, new(2026, hireMonth, 10), Born1990, 2026));

    [Fact]
    public void Hired_after_the_year_gets_nothing()
        => Assert.Equal(0m, LeaveCalculator.Entitlement(21, 30, new(2027, 1, 1), Born1990, 2026));

    [Fact]
    public void Type_without_yearly_days_has_no_entitlement()
        => Assert.Equal(0m, LeaveCalculator.Entitlement(0, null, new(2020, 1, 1), Born1990, 2026));
}
