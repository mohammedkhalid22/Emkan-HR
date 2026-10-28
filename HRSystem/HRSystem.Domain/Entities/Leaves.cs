using HRSystem.Domain.Common;
using HRSystem.Domain.Enums;

namespace HRSystem.Domain.Entities;

public class LeaveType : BaseEntity
{
    public string Name { get; set; } = null!;
    // 0 = no yearly balance (e.g. unpaid or sick leave): requests are recorded but not limited
    public int AnnualDays { get; set; }

    // Days for employees with 10+ years of service or aged 50+ (null = same as AnnualDays)
    public int? SeniorAnnualDays { get; set; }

    public bool IsPaid { get; set; } = true;
    public bool RequiresAttachment { get; set; }
}

public class LeaveBalance : BaseEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public int LeaveTypeId { get; set; }
    public LeaveType LeaveType { get; set; } = null!;

    public int Year { get; set; }
    public decimal Entitled { get; set; }
    public decimal Used { get; set; }

    public decimal Remaining => Entitled - Used;
}

public class LeaveRequest : BaseEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public int LeaveTypeId { get; set; }
    public LeaveType LeaveType { get; set; } = null!;

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal Days { get; set; }
    public string? Reason { get; set; }
    public string? AttachmentPath { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Pending;

    public string? ManagerActionBy { get; set; }
    public DateTime? ManagerActionAt { get; set; }
    public string? HrActionBy { get; set; }
    public DateTime? HrActionAt { get; set; }
    public string? RejectionReason { get; set; }
}

public class PublicHoliday : BaseEntity
{
    public DateOnly Date { get; set; }
    public string Name { get; set; } = null!;
}
