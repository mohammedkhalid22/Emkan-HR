using HRSystem.Domain.Common;
using HRSystem.Domain.Enums;

namespace HRSystem.Domain.Entities;

public class Shift : BaseEntity
{
    public string Name { get; set; } = null!;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int GraceMinutes { get; set; } = 15;

    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}

public class AttendanceRecord : BaseEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public DateOnly Date { get; set; }
    public TimeOnly? CheckIn { get; set; }
    public TimeOnly? CheckOut { get; set; }
    public int LateMinutes { get; set; }
    public int EarlyLeaveMinutes { get; set; }
    public int OvertimeMinutes { get; set; }
    public AttendanceStatus Status { get; set; }
    public AttendanceSource Source { get; set; }

    // Where the employee was when checking in/out from the app, and how far from the branch
    public double? CheckInLatitude { get; set; }
    public double? CheckInLongitude { get; set; }
    public int? CheckInDistanceMeters { get; set; }
    public double? CheckOutLatitude { get; set; }
    public double? CheckOutLongitude { get; set; }
    public int? CheckOutDistanceMeters { get; set; }

    // Selfies taken with the phone at check-in / check-out (paths in file storage)
    public string? CheckInPhotoPath { get; set; }
    public string? CheckOutPhotoPath { get; set; }

    // Why HR edited the record by hand
    public string? Notes { get; set; }
}

// A fingerprint device on the company network (ZKTeco), read over IP
public class AttendanceDevice : BaseEntity
{
    public string Name { get; set; } = null!;
    public string IpAddress { get; set; } = null!;
    public int Port { get; set; } = 4370;

    // The device's "Comm Key" (0 when none is set on the device)
    public int CommKey { get; set; }
    public bool IsEnabled { get; set; } = true;

    // True: this server reaches the device directly (same network).
    // False: the small agent at the company reads it and sends the punches here with ApiKey.
    public bool PulledByServer { get; set; }
    public string ApiKey { get; set; } = null!;

    public DateTime? LastSyncAt { get; set; }
    public bool? LastSyncSucceeded { get; set; }
    public string? LastSyncMessage { get; set; }
}

// One raw punch as the device recorded it; turned into the day's check-in / check-out
public class AttendancePunch
{
    public long Id { get; set; }
    public int DeviceId { get; set; }
    public string FingerprintId { get; set; } = null!;
    public DateTime PunchTime { get; set; }
    public DateTime ReceivedAt { get; set; }
}

public class AttendanceRequest : BaseEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public AttendanceRequestType Type { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly FromTime { get; set; }
    public TimeOnly ToTime { get; set; }
    public string? Reason { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public string? RejectionReason { get; set; }
}
