using System.ComponentModel.DataAnnotations;
using HRSystem.Domain.Enums;

namespace HRSystem.BLL.DTOs;

// Who is asking decides which employees they can see
public record EmployeeAccessScope(bool SeeAll, int? OwnEmployeeId, bool SeeTeam)
{
    public bool CanSee(int employeeId, int? managerId)
        => SeeAll || employeeId == OwnEmployeeId || (SeeTeam && OwnEmployeeId != null && managerId == OwnEmployeeId);
}

public class EmployeeFilterDto
{
    public string? Search { get; set; }
    public int? DepartmentId { get; set; }
    public EmployeeStatus? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public class EmployeeListItemDto
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string FullNameAr { get; set; } = null!;
    public string NationalId { get; set; } = null!;
    public string DepartmentName { get; set; } = null!;
    public string JobTitleName { get; set; } = null!;
    public string? ManagerName { get; set; }
    public string? Phone { get; set; }
    public DateOnly HireDate { get; set; }
    public EmployeeStatus Status { get; set; }
    public bool HasPhoto { get; set; }
}

public class EmployeeDetailsDto
{
    public int Id { get; set; }
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
    public bool HasPhoto { get; set; }

    public DateOnly HireDate { get; set; }
    public ContractType ContractType { get; set; }
    public DateOnly? ContractEndDate { get; set; }
    public EmployeeStatus Status { get; set; }
    public DateOnly? TerminationDate { get; set; }
    public string? TerminationReason { get; set; }

    public string DepartmentName { get; set; } = null!;
    public string JobTitleName { get; set; } = null!;
    public int? ManagerId { get; set; }
    public string? ManagerName { get; set; }
    public string? ShiftName { get; set; }

    public decimal BasicSalary { get; set; }
    public decimal InsuranceSalary { get; set; }
    public string? BankAccount { get; set; }

    public string? UserId { get; set; }

    public List<EmployeeDocumentDto> Documents { get; set; } = [];
    public List<EmployeeHistoryDto> History { get; set; } = [];
}

public class EmployeeDocumentDto
{
    public int Id { get; set; }
    public string DocumentType { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public DateOnly? ExpiryDate { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class EmployeeHistoryDto
{
    public string ChangeType { get; set; } = null!;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateOnly EffectiveDate { get; set; }
}

public class SaveEmployeeDto
{
    public int Id { get; set; }

    // ----- Personal -----

    [Display(Name = "الاسم بالعربي")]
    [Required(ErrorMessage = "{0} مطلوب")]
    [StringLength(150, ErrorMessage = "{0} لازم يكون {1} حرف أو أقل")]
    public string FullNameAr { get; set; } = null!;

    [Display(Name = "الاسم بالإنجليزي")]
    [StringLength(150, ErrorMessage = "{0} لازم يكون {1} حرف أو أقل")]
    public string? FullNameEn { get; set; }

    [Display(Name = "الرقم القومي")]
    [Required(ErrorMessage = "{0} مطلوب")]
    [RegularExpression(@"^\d{14}$", ErrorMessage = "{0} لازم يكون 14 رقم")]
    public string NationalId { get; set; } = null!;

    [Display(Name = "تاريخ الميلاد")]
    [Required(ErrorMessage = "{0} مطلوب")]
    public DateOnly? BirthDate { get; set; }

    [Display(Name = "النوع")]
    [Required(ErrorMessage = "{0} مطلوب")]
    public Gender? Gender { get; set; }

    [Display(Name = "الحالة الاجتماعية")]
    public MaritalStatus? MaritalStatus { get; set; }

    [Display(Name = "الموبايل")]
    [RegularExpression(@"^01[0125]\d{8}$", ErrorMessage = "{0} لازم يكون رقم مصري من 11 رقم يبدأ بـ 010 أو 011 أو 012 أو 015")]
    public string? Phone { get; set; }

    [Display(Name = "البريد الإلكتروني")]
    [EmailAddress(ErrorMessage = "{0} مش صحيح")]
    [StringLength(150)]
    public string? Email { get; set; }

    [Display(Name = "العنوان")]
    [StringLength(250, ErrorMessage = "{0} لازم يكون {1} حرف أو أقل")]
    public string? Address { get; set; }

    // ----- Job -----

    [Display(Name = "القسم")]
    [Required(ErrorMessage = "{0} مطلوب")]
    public int? DepartmentId { get; set; }

    [Display(Name = "المسمى الوظيفي")]
    [Required(ErrorMessage = "{0} مطلوب")]
    public int? JobTitleId { get; set; }

    [Display(Name = "المدير المباشر")]
    public int? ManagerId { get; set; }

    [Display(Name = "الوردية")]
    public int? ShiftId { get; set; }

    [Display(Name = "تاريخ التعيين")]
    [Required(ErrorMessage = "{0} مطلوب")]
    public DateOnly? HireDate { get; set; }

    [Display(Name = "نوع العقد")]
    [Required(ErrorMessage = "{0} مطلوب")]
    public ContractType? ContractType { get; set; }

    [Display(Name = "تاريخ انتهاء العقد")]
    public DateOnly? ContractEndDate { get; set; }

    // ----- Financial -----

    [Display(Name = "المرتب الأساسي")]
    [Required(ErrorMessage = "{0} مطلوب")]
    [Range(0, 10_000_000, ErrorMessage = "{0} لازم يكون بين {1} و {2}")]
    public decimal? BasicSalary { get; set; }

    // Empty = same as the basic salary
    [Display(Name = "أجر الاشتراك التأميني")]
    [Range(0, 10_000_000, ErrorMessage = "{0} لازم يكون بين {1} و {2}")]
    public decimal? InsuranceSalary { get; set; }

    [Display(Name = "رقم الحساب البنكي")]
    [StringLength(34, ErrorMessage = "{0} لازم يكون {1} حرف أو أقل")]
    public string? BankAccount { get; set; }
}

public class TerminateEmployeeDto
{
    public int EmployeeId { get; set; }

    [Display(Name = "تاريخ إنهاء الخدمة")]
    [Required(ErrorMessage = "{0} مطلوب")]
    public DateOnly? TerminationDate { get; set; }

    [Display(Name = "السبب")]
    [Required(ErrorMessage = "{0} مطلوب")]
    [StringLength(250, ErrorMessage = "{0} لازم يكون {1} حرف أو أقل")]
    public string Reason { get; set; } = null!;
}

public class UploadDocumentDto
{
    public int EmployeeId { get; set; }

    [Display(Name = "نوع المستند")]
    [Required(ErrorMessage = "{0} مطلوب")]
    [StringLength(50)]
    public string DocumentType { get; set; } = null!;

    [Display(Name = "تاريخ الانتهاء")]
    public DateOnly? ExpiryDate { get; set; }
}

public class EmployeeAccountDto
{
    public string UserId { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? Role { get; set; }
    public bool IsActive { get; set; }
    public bool IsLockedOut { get; set; }
}

public class CreateAccountDto
{
    public int EmployeeId { get; set; }

    [Display(Name = "البريد الإلكتروني")]
    [Required(ErrorMessage = "{0} مطلوب")]
    [EmailAddress(ErrorMessage = "{0} مش صحيح")]
    public string Email { get; set; } = null!;

    [Display(Name = "الدور")]
    [Required(ErrorMessage = "{0} مطلوب")]
    public string Role { get; set; } = null!;

    [Display(Name = "كلمة السر")]
    [Required(ErrorMessage = "{0} مطلوبة")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "{0} لازم تكون {2} حروف على الأقل")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = null!;
}
