using System.ComponentModel.DataAnnotations;
using HRSystem.Domain.Enums;

namespace HRSystem.BLL.DTOs;

// ---------- Shifts ----------

public class ShiftDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int GraceMinutes { get; set; }
    public int EmployeesCount { get; set; }
}

public class SaveShiftDto
{
    public int Id { get; set; }

    [Display(Name = "اسم الوردية")]
    [Required(ErrorMessage = "{0} مطلوب")]
    [StringLength(50, ErrorMessage = "{0} لازم يكون {1} حرف أو أقل")]
    public string Name { get; set; } = null!;

    [Display(Name = "بداية الوردية")]
    [Required(ErrorMessage = "{0} مطلوبة")]
    public TimeOnly? StartTime { get; set; }

    [Display(Name = "نهاية الوردية")]
    [Required(ErrorMessage = "{0} مطلوبة")]
    public TimeOnly? EndTime { get; set; }

    [Display(Name = "فترة السماح (دقايق)")]
    [Range(0, 120, ErrorMessage = "{0} لازم تكون بين {1} و {2}")]
    public int GraceMinutes { get; set; } = 15;
}

// ---------- Records ----------

public class AttendanceFilterDto
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? DepartmentId { get; set; }
    public AttendanceStatus? Status { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class AttendanceRecordDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string DepartmentName { get; set; } = null!;
    public DateOnly Date { get; set; }
    public TimeOnly? CheckIn { get; set; }
    public TimeOnly? CheckOut { get; set; }
    public int LateMinutes { get; set; }
    public int EarlyLeaveMinutes { get; set; }
    public int OvertimeMinutes { get; set; }
    public AttendanceStatus Status { get; set; }
    public AttendanceSource Source { get; set; }
    public string? Notes { get; set; }

    public double? CheckInLatitude { get; set; }
    public double? CheckInLongitude { get; set; }
    public int? CheckInDistanceMeters { get; set; }
    public double? CheckOutLatitude { get; set; }
    public double? CheckOutLongitude { get; set; }
    public int? CheckOutDistanceMeters { get; set; }
    public int? BranchRadiusMeters { get; set; }
}

public class TodayAttendanceDto
{
    public DateOnly Date { get; set; }
    public string? ShiftName { get; set; }
    public TimeOnly? ShiftStart { get; set; }
    public TimeOnly? ShiftEnd { get; set; }
    public TimeOnly? CheckIn { get; set; }
    public TimeOnly? CheckOut { get; set; }
    public int LateMinutes { get; set; }
    public bool IsWorkingDay { get; set; }
}

public class ManualAttendanceDto
{
    public int EmployeeId { get; set; }
    public string? EmployeeName { get; set; }

    [Display(Name = "التاريخ")]
    [Required(ErrorMessage = "{0} مطلوب")]
    public DateOnly? Date { get; set; }

    [Display(Name = "وقت الحضور")]
    public TimeOnly? CheckIn { get; set; }

    [Display(Name = "وقت الانصراف")]
    public TimeOnly? CheckOut { get; set; }

    [Display(Name = "الحالة")]
    [Required(ErrorMessage = "{0} مطلوبة")]
    public AttendanceStatus? Status { get; set; }

    [Display(Name = "سبب التعديل")]
    [Required(ErrorMessage = "{0} مطلوب")]
    [StringLength(250, ErrorMessage = "{0} لازم يكون {1} حرف أو أقل")]
    public string Notes { get; set; } = null!;
}

public class MonthlyAttendanceRowDto
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string DepartmentName { get; set; } = null!;
    public int PresentDays { get; set; }
    public int AbsentDays { get; set; }
    public int LeaveDays { get; set; }
    public int MissionDays { get; set; }
    public int HolidayDays { get; set; }
    public int LateMinutes { get; set; }
    public int EarlyLeaveMinutes { get; set; }
    public int OvertimeMinutes { get; set; }
}

// ---------- Fingerprint import ----------

// One row as read from the file (text, before validation)
public record AttendanceImportRow(int RowNumber, string? Code, string? Date, string? CheckIn, string? CheckOut);

public class AttendanceImportResultDto
{
    public int RowNumber { get; set; }
    public string? Code { get; set; }
    public string? EmployeeName { get; set; }
    public DateOnly? Date { get; set; }
    public TimeOnly? CheckIn { get; set; }
    public TimeOnly? CheckOut { get; set; }
    public bool IsValid { get; set; }
    public string? Message { get; set; }
}

// ---------- Permissions / missions ----------

public class AttendanceRequestDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = null!;
    public string DepartmentName { get; set; } = null!;
    public int? EmployeeManagerId { get; set; }
    public AttendanceRequestType Type { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly FromTime { get; set; }
    public TimeOnly ToTime { get; set; }
    public string? Reason { get; set; }
    public RequestStatus Status { get; set; }
    public string? RejectionReason { get; set; }

    public bool CanApprove { get; set; }
    public bool CanCancel { get; set; }
}

public class CreateAttendanceRequestDto
{
    [Display(Name = "النوع")]
    [Required(ErrorMessage = "{0} مطلوب")]
    public AttendanceRequestType? Type { get; set; }

    [Display(Name = "التاريخ")]
    [Required(ErrorMessage = "{0} مطلوب")]
    public DateOnly? Date { get; set; }

    [Display(Name = "من الساعة")]
    [Required(ErrorMessage = "{0} مطلوب")]
    public TimeOnly? FromTime { get; set; }

    [Display(Name = "إلى الساعة")]
    [Required(ErrorMessage = "{0} مطلوب")]
    public TimeOnly? ToTime { get; set; }

    [Display(Name = "السبب")]
    [Required(ErrorMessage = "{0} مطلوب")]
    [StringLength(500, ErrorMessage = "{0} لازم يكون {1} حرف أو أقل")]
    public string Reason { get; set; } = null!;
}
