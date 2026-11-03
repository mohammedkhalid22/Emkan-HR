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
