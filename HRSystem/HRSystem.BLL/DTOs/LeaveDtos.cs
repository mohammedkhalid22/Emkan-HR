using System.ComponentModel.DataAnnotations;
using HRSystem.Domain.Enums;

namespace HRSystem.BLL.DTOs;

// Who is acting on a request
public record ApproverContext(string? UserId, int? EmployeeId, bool IsHr, bool IsManager);

// ---------- Leave types ----------

public class LeaveTypeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int AnnualDays { get; set; }
    public int? SeniorAnnualDays { get; set; }
    public bool IsPaid { get; set; }
    public bool RequiresAttachment { get; set; }
    public int RequestsCount { get; set; }
}

public class SaveLeaveTypeDto
{
    public int Id { get; set; }

    [Display(Name = "اسم النوع")]
    [Required(ErrorMessage = "{0} مطلوب")]
    [StringLength(50, ErrorMessage = "{0} لازم يكون {1} حرف أو أقل")]
    public string Name { get; set; } = null!;

    [Display(Name = "الرصيد السنوي (أيام)")]
    [Range(0, 365, ErrorMessage = "{0} لازم يكون بين {1} و {2}")]
    public int AnnualDays { get; set; }

    [Display(Name = "رصيد الأقدمية (10 سنين خدمة أو سن 50)")]
    [Range(0, 365, ErrorMessage = "{0} لازم يكون بين {1} و {2}")]
    public int? SeniorAnnualDays { get; set; }

    [Display(Name = "مدفوعة الأجر")]
    public bool IsPaid { get; set; } = true;

    [Display(Name = "محتاجة مرفق")]
    public bool RequiresAttachment { get; set; }
}

// ---------- Public holidays ----------

public class PublicHolidayDto
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string Name { get; set; } = null!;
}

public class SavePublicHolidayDto
{
    public int Id { get; set; }

    [Display(Name = "التاريخ")]
    [Required(ErrorMessage = "{0} مطلوب")]
    public DateOnly? Date { get; set; }

    [Display(Name = "اسم العطلة")]
    [Required(ErrorMessage = "{0} مطلوب")]
    [StringLength(100, ErrorMessage = "{0} لازم يكون {1} حرف أو أقل")]
    public string Name { get; set; } = null!;
}

// ---------- Balances ----------

public class LeaveBalanceFilterDto
{
    public int Year { get; set; } = DateTime.Today.Year;
    public int? DepartmentId { get; set; }
    public int? LeaveTypeId { get; set; }
    public string? Search { get; set; }
}

public class LeaveBalanceRowDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string DepartmentName { get; set; } = null!;
    public string LeaveTypeName { get; set; } = null!;
    public decimal Entitled { get; set; }
    public decimal Used { get; set; }
    public decimal Pending { get; set; }
    public decimal Available => Entitled - Used - Pending;
}

public class MyBalanceDto
{
    public int LeaveTypeId { get; set; }
    public string LeaveTypeName { get; set; } = null!;
    public bool IsLimited { get; set; }
    public decimal Entitled { get; set; }
    public decimal Used { get; set; }
    public decimal Pending { get; set; }
    public decimal Available => Entitled - Used - Pending;
}

// ---------- Requests ----------

public class LeaveRequestFilterDto
{
    public RequestStatus? Status { get; set; }
    public int? Year { get; set; }
    public int? DepartmentId { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public class LeaveRequestDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string DepartmentName { get; set; } = null!;
    public int? EmployeeManagerId { get; set; }
    public string LeaveTypeName { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal Days { get; set; }
    public string? Reason { get; set; }
    public RequestStatus Status { get; set; }
    public string? RejectionReason { get; set; }
    public bool HasAttachment { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ManagerActionAt { get; set; }
    public DateTime? HrActionAt { get; set; }

    // Filled for the viewer
    public bool CanApprove { get; set; }
    public bool CanCancel { get; set; }
}

public class CreateLeaveRequestDto
{
    [Display(Name = "نوع الإجازة")]
    [Required(ErrorMessage = "{0} مطلوب")]
    public int? LeaveTypeId { get; set; }

    [Display(Name = "من")]
    [Required(ErrorMessage = "تاريخ البداية مطلوب")]
    public DateOnly? StartDate { get; set; }

    [Display(Name = "إلى")]
    [Required(ErrorMessage = "تاريخ النهاية مطلوب")]
    public DateOnly? EndDate { get; set; }

    [Display(Name = "السبب")]
    [StringLength(500, ErrorMessage = "{0} لازم يكون {1} حرف أو أقل")]
    public string? Reason { get; set; }
}

public class LeavePreviewDto
{
    public int Days { get; set; }
    public bool IsLimited { get; set; }
    public decimal? Available { get; set; }
    public bool RequiresAttachment { get; set; }
    public string? Error { get; set; }
}
