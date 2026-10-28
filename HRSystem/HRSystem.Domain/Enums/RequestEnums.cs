namespace HRSystem.Domain.Enums;

// Shared by leave requests and attendance requests (permissions / missions)
public enum RequestStatus : byte
{
    Pending = 1,
    ManagerApproved = 2,
    Approved = 3,
    Rejected = 4,
    Cancelled = 5
}

public enum AttendanceStatus : byte
{
    Present = 1,
    Absent = 2,
    OnLeave = 3,
    Holiday = 4,
    Mission = 5
}

public enum AttendanceSource : byte
{
    Manual = 1,
    Device = 2,
    Web = 3,

    // Created by the daily close for days without a check-in (absent, on leave, holiday, mission)
    System = 4
}

public enum AttendanceRequestType : byte
{
    Permission = 1,
    Mission = 2
}
