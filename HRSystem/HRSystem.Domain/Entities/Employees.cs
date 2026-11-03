using HRSystem.Domain.Common;
using HRSystem.Domain.Enums;
using HRSystem.Domain.Identity;

namespace HRSystem.Domain.Entities;

public class Employee : BaseEntity
{
    public string Code { get; set; } = null!;
    public string FullNameAr { get; set; } = null!;
    public string? FullNameEn { get; set; }
    public string NationalId { get; set; } = null!;
    public DateOnly BirthDate { get; set; }
    public Gender Gender { get; set; }
    public MaritalStatus? MaritalStatus { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? PhotoPath { get; set; }

    public DateOnly HireDate { get; set; }
    public ContractType ContractType { get; set; }
    public DateOnly? ContractEndDate { get; set; }
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
    public DateOnly? TerminationDate { get; set; }
    public string? TerminationReason { get; set; }

    public decimal BasicSalary { get; set; }
    public decimal InsuranceSalary { get; set; }
    public string? BankAccount { get; set; }

    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    public int JobTitleId { get; set; }
    public JobTitle JobTitle { get; set; } = null!;

    public int? ManagerId { get; set; }
    public Employee? Manager { get; set; }
    public ICollection<Employee> Subordinates { get; set; } = new List<Employee>();

    public int? ShiftId { get; set; }
    public Shift? Shift { get; set; }

    // Field / remote staff: may check in from anywhere (the location is still recorded)
    public bool AllowRemoteAttendance { get; set; }

    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public ICollection<EmployeeDocument> Documents { get; set; } = new List<EmployeeDocument>();
    public ICollection<EmployeeHistory> History { get; set; } = new List<EmployeeHistory>();
    public ICollection<LeaveBalance> LeaveBalances { get; set; } = new List<LeaveBalance>();
    public ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
}

// What the AI made of the employee's CV, kept so it isn't sent again on every visit
public class CvAnalysis : BaseEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public int? EmployeeDocumentId { get; set; }
    public EmployeeDocument? EmployeeDocument { get; set; }

    // The job the CV was scored against, as it was at the time
    public string JobTitleName { get; set; } = null!;
    public bool HadRequirements { get; set; }

    public string Summary { get; set; } = null!;
    public decimal? ExperienceYears { get; set; }
    public string? Education { get; set; }
    public int MatchPercent { get; set; }
    public string? MatchReason { get; set; }

    // One item per line
    public string? Skills { get; set; }
    public string? Strengths { get; set; }
    public string? Gaps { get; set; }

    public string Model { get; set; } = null!;
}

public class EmployeeDocument : BaseEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public string DocumentType { get; set; } = null!;
    public string FilePath { get; set; } = null!;
    public DateOnly? ExpiryDate { get; set; }
}

public class EmployeeHistory : BaseEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public string ChangeType { get; set; } = null!;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateOnly EffectiveDate { get; set; }
}
