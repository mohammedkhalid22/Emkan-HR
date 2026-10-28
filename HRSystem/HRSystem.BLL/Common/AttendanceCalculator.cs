namespace HRSystem.BLL.Common;

public record ShiftTimes(TimeOnly Start, TimeOnly End, int GraceMinutes);

public record TimeRange(TimeOnly From, TimeOnly To);

public record AttendanceResult(int LateMinutes, int EarlyLeaveMinutes, int OvertimeMinutes);

// Pure calculations (no database) so they can be unit tested
public static class AttendanceCalculator
{
    // permissions = approved permissions for that day (they excuse lateness / leaving early)
    public static AttendanceResult Calculate(ShiftTimes shift, TimeOnly? checkIn, TimeOnly? checkOut,
        bool isWorkingDay, IReadOnlyList<TimeRange> permissions, int overtimeMinimumMinutes)
    {
        if (!isWorkingDay)
        {
            // Weekend or holiday: everything worked is overtime
            var worked = checkIn is TimeOnly i && checkOut is TimeOnly o ? Math.Max(0, Minutes(o, i)) : 0;
            return new AttendanceResult(0, 0, worked >= overtimeMinimumMinutes ? worked : 0);
        }

        return new AttendanceResult(
            LateMinutes(shift, checkIn, permissions),
            EarlyLeaveMinutes(shift, checkOut, permissions),
            OvertimeMinutes(shift, checkOut, overtimeMinimumMinutes));
    }

    // Arriving after (start + grace) counts every minute from the start; within the grace period counts nothing.
    // A permission that starts at the beginning of the shift moves the start to the end of the permission.
    private static int LateMinutes(ShiftTimes shift, TimeOnly? checkIn, IReadOnlyList<TimeRange> permissions)
    {
        if (checkIn is not TimeOnly arrived)
            return 0;

        var start = shift.Start;
        foreach (var p in permissions)
        {
            if (p.From <= shift.Start.AddMinutes(shift.GraceMinutes) && p.To > start)
                start = p.To;
        }

        return Minutes(arrived, start) > shift.GraceMinutes ? Minutes(arrived, start) : 0;
    }

    // A permission that reaches the end of the shift moves the end back to the start of the permission
    private static int EarlyLeaveMinutes(ShiftTimes shift, TimeOnly? checkOut, IReadOnlyList<TimeRange> permissions)
    {
        if (checkOut is not TimeOnly left)
            return 0;

        var end = shift.End;
        foreach (var p in permissions)
        {
            if (p.To >= shift.End.AddMinutes(-shift.GraceMinutes) && p.From < end)
                end = p.From;
        }

        return Math.Max(0, Minutes(end, left));
    }

    private static int OvertimeMinutes(ShiftTimes shift, TimeOnly? checkOut, int minimum)
    {
        if (checkOut is not TimeOnly left)
            return 0;

        var extra = Minutes(left, shift.End);
        return extra >= minimum ? extra : 0;
    }

    // TimeOnly subtraction wraps around midnight, so compare as plain time spans
    private static int Minutes(TimeOnly later, TimeOnly earlier)
        => (int)(later.ToTimeSpan() - earlier.ToTimeSpan()).TotalMinutes;
}
