using HRSystem.BLL.Common;

namespace HRSystem.Tests;

public class AttendanceCalculatorTests
{
    // 9:00 to 17:00, 15 minutes grace, overtime counted from 30 minutes
    private static readonly ShiftTimes Morning = new(new(9, 0), new(17, 0), 15);
    private static readonly List<TimeRange> NoPermissions = [];
    private const int OvertimeMinimum = 30;

    private static AttendanceResult Calc(TimeOnly? checkIn, TimeOnly? checkOut, bool workingDay = true, List<TimeRange>? permissions = null)
        => AttendanceCalculator.Calculate(Morning, checkIn, checkOut, workingDay, permissions ?? NoPermissions, OvertimeMinimum);

    [Fact]
    public void Arriving_at_9_20_is_20_minutes_late()
        => Assert.Equal(20, Calc(new(9, 20), new(17, 0)).LateMinutes);

    [Fact]
    public void Arriving_at_9_10_is_within_grace()
        => Assert.Equal(0, Calc(new(9, 10), new(17, 0)).LateMinutes);

    [Fact]
    public void Arriving_exactly_at_the_end_of_grace_is_not_late()
        => Assert.Equal(0, Calc(new(9, 15), new(17, 0)).LateMinutes);

    [Fact]
    public void Leaving_at_16_30_is_30_minutes_early()
        => Assert.Equal(30, Calc(new(9, 0), new(16, 30)).EarlyLeaveMinutes);

    [Fact]
    public void Staying_45_minutes_is_overtime()
        => Assert.Equal(45, Calc(new(9, 0), new(17, 45)).OvertimeMinutes);

    [Fact]
    public void Staying_20_minutes_is_below_the_overtime_minimum()
        => Assert.Equal(0, Calc(new(9, 0), new(17, 20)).OvertimeMinutes);

    [Fact]
    public void Morning_permission_excuses_lateness()
    {
        List<TimeRange> permission = [new(new(9, 0), new(11, 0))];
        Assert.Equal(0, Calc(new(10, 50), new(17, 0), permissions: permission).LateMinutes);
    }

    [Fact]
    public void Arriving_after_the_permission_ends_counts_from_its_end()
    {
        List<TimeRange> permission = [new(new(9, 0), new(11, 0))];
        Assert.Equal(30, Calc(new(11, 30), new(17, 0), permissions: permission).LateMinutes);
    }

    [Fact]
    public void Afternoon_permission_excuses_leaving_early()
    {
        List<TimeRange> permission = [new(new(15, 0), new(17, 0))];
        Assert.Equal(0, Calc(new(9, 0), new(15, 0), permissions: permission).EarlyLeaveMinutes);
    }

    [Fact]
    public void Weekend_work_is_all_overtime()
    {
        var result = Calc(new(10, 0), new(14, 0), workingDay: false);
        Assert.Equal(new AttendanceResult(0, 0, 240), result);
    }

    [Fact]
    public void Missing_check_out_gives_no_early_leave_or_overtime()
    {
        var result = Calc(new(9, 30), null);
        Assert.Equal(new AttendanceResult(30, 0, 0), result);
    }
}
