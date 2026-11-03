using HRSystem.BLL.Common;

namespace HRSystem.Tests;

public class HrAlertsTests
{
    [Fact]
    public void Birthday_matches_day_and_month_in_any_year()
        => Assert.True(HrAlerts.IsBirthday(new DateOnly(1990, 11, 3), new DateOnly(2026, 11, 3)));

    [Fact]
    public void Another_day_is_not_a_birthday()
        => Assert.False(HrAlerts.IsBirthday(new DateOnly(1990, 11, 3), new DateOnly(2026, 11, 4)));

    [Fact]
    public void Born_on_29_February_celebrates_on_28_February_in_normal_years()
    {
        var born = new DateOnly(1996, 2, 29);
        Assert.True(HrAlerts.IsBirthday(born, new DateOnly(2026, 2, 28)));
        Assert.False(HrAlerts.IsBirthday(born, new DateOnly(2028, 2, 28)));
        Assert.True(HrAlerts.IsBirthday(born, new DateOnly(2028, 2, 29)));
    }

    [Fact]
    public void Anniversary_counts_whole_years_on_the_hire_day()
        => Assert.Equal(5, HrAlerts.AnniversaryYears(new DateOnly(2021, 11, 3), new DateOnly(2026, 11, 3)));

    [Fact]
    public void The_hire_day_itself_is_not_an_anniversary()
        => Assert.Null(HrAlerts.AnniversaryYears(new DateOnly(2026, 11, 3), new DateOnly(2026, 11, 3)));

    [Fact]
    public void A_day_off_the_hire_date_is_not_an_anniversary()
        => Assert.Null(HrAlerts.AnniversaryYears(new DateOnly(2021, 11, 3), new DateOnly(2026, 11, 2)));

    [Theory]
    [InlineData("2026-11-03", true)]
    [InlineData("2026-12-03", true)]
    [InlineData("2026-12-04", false)]
    [InlineData("2026-11-02", false)]
    public void Warning_window_runs_from_today_to_30_days_ahead(string date, bool expected)
        => Assert.Equal(expected, HrAlerts.IsWithin(DateOnly.Parse(date), new DateOnly(2026, 11, 3), 30));

    [Fact]
    public void Dates_read_in_arabic()
        => Assert.Equal("16 نوفمبر", HrAlerts.Day(new DateOnly(2026, 11, 16)));
}
