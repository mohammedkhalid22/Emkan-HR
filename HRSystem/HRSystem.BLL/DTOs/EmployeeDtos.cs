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
    public string? PositionName { get; set; }
    public int? ManagerId { get; set; }
    public string? ManagerName { get; set; }
    public string? ShiftName { get; set; }
    public string? FingerprintId { get; set; }

    public decimal BasicSalary { get; set; }
    public decimal InsuranceSalary { get; set; }
    public string? BankAccount { get; set; }
    public string? BankName { get; set; }
    public string? BankBranch { get; set; }
    public string? Iban { get; set; }

    public string? Nationality { get; set; }
    public Religion? Religion { get; set; }
    public MilitaryStatus? MilitaryStatus { get; set; }
    public string? BloodType { get; set; }
    public string? InsuranceNumber { get; set; }
    public DateOnly? InsuranceStartDate { get; set; }
    public string? PassportNumber { get; set; }
    public DateOnly? PassportExpiry { get; set; }
    public string? DrivingLicenseNumber { get; set; }
    public DateOnly? DrivingLicenseExpiry { get; set; }

    public string? Governorate { get; set; }
    public string? City { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactRelation { get; set; }
    public string? EmergencyContactPhone { get; set; }

    public string? UserId { get; set; }

    public List<EmployeeDocumentDto> Documents { get; set; } = [];
    public List<EmployeeHistoryDto> History { get; set; } = [];
    public List<FamilyMemberDto> Family { get; set; } = [];
    public List<QualificationDto> Qualifications { get; set; } = [];
    public List<ExperienceDto> Experiences { get; set; } = [];
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

    [Display(Name = "مسموح له يسجل حضور من أي مكان")]
    public bool AllowRemoteAttendance { get; set; }

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

    [Display(Name = "البنك")]
    [StringLength(100, ErrorMessage = "{0} لازم يكون {1} حرف أو أقل")]
    public string? BankName { get; set; }

    [Display(Name = "فرع البنك")]
    [StringLength(100, ErrorMessage = "{0} لازم يكون {1} حرف أو أقل")]
    public string? BankBranch { get; set; }

    [Display(Name = "IBAN")]
    [RegularExpression(@"^[A-Za-z]{2}\d{2}[A-Za-z0-9]{10,30}$", ErrorMessage = "{0} مش صحيح (زي EG380019000500000000263180002)")]
    public string? Iban { get; set; }

    // ----- Position and fingerprint -----

    [Display(Name = "الموقع الوظيفي")]
    public int? PositionId { get; set; }

    [Display(Name = "رقم البصمة")]
    [RegularExpression(@"^\d{1,9}$", ErrorMessage = "{0} أرقام بس (الرقم اللي الموظف متسجل بيه على الجهاز)")]
    public string? FingerprintId { get; set; }

    // ----- Official -----

    [Display(Name = "الجنسية")]
    [StringLength(50, ErrorMessage = "{0} لازم تكون {1} حرف أو أقل")]
    public string? Nationality { get; set; }

    [Display(Name = "الديانة")]
    public Religion? Religion { get; set; }

    [Display(Name = "الموقف من التجنيد")]
    public MilitaryStatus? MilitaryStatus { get; set; }

    [Display(Name = "فصيلة الدم")]
    [RegularExpression(@"^(A|B|AB|O)[+-]$", ErrorMessage = "{0} زي A+ أو O-")]
    public string? BloodType { get; set; }

    [Display(Name = "الرقم التأميني")]
    [RegularExpression(@"^\d{1,20}$", ErrorMessage = "{0} أرقام بس")]
    public string? InsuranceNumber { get; set; }

    [Display(Name = "تاريخ الاشتراك في التأمينات")]
    public DateOnly? InsuranceStartDate { get; set; }

    [Display(Name = "رقم جواز السفر")]
    [StringLength(20, ErrorMessage = "{0} لازم يكون {1} حرف أو أقل")]
    public string? PassportNumber { get; set; }

    [Display(Name = "انتهاء جواز السفر")]
    public DateOnly? PassportExpiry { get; set; }

    [Display(Name = "رقم رخصة القيادة")]
    [StringLength(30, ErrorMessage = "{0} لازم يكون {1} حرف أو أقل")]
    public string? DrivingLicenseNumber { get; set; }

    [Display(Name = "انتهاء رخصة القيادة")]
    public DateOnly? DrivingLicenseExpiry { get; set; }

    // ----- Home and emergency -----

    [Display(Name = "المحافظة")]
    [StringLength(50, ErrorMessage = "{0} لازم تكون {1} حرف أو أقل")]
    public string? Governorate { get; set; }

    [Display(Name = "المدينة / الحي")]
    [StringLength(50, ErrorMessage = "{0} لازم تكون {1} حرف أو أقل")]
    public string? City { get; set; }

    [Display(Name = "اسم شخص للطوارئ")]
    [StringLength(150, ErrorMessage = "{0} لازم يكون {1} حرف أو أقل")]
    public string? EmergencyContactName { get; set; }

    [Display(Name = "صلة القرابة")]
    [StringLength(50, ErrorMessage = "{0} لازم تكون {1} حرف أو أقل")]
    public string? EmergencyContactRelation { get; set; }

    [Display(Name = "موبايل الطوارئ")]
    [RegularExpression(@"^\+?\d{8,15}$", ErrorMessage = "{0} مش صحيح")]
    public string? EmergencyContactPhone { get; set; }
}

// ---------- Family, qualifications, experience (lists on the profile) ----------

public class FamilyMemberDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }

    [Display(Name = "الاسم")]
    [Required(ErrorMessage = "{0} مطلوب")]
    [StringLength(150, ErrorMessage = "{0} لازم يكون {1} حرف أو أقل")]
    public string Name { get; set; } = null!;

    [Display(Name = "صلة القرابة")]
    [Required(ErrorMessage = "{0} مطلوبة")]
    public FamilyRelation? Relation { get; set; }

    [Display(Name = "تاريخ الميلاد")]
    public DateOnly? BirthDate { get; set; }

    [Display(Name = "الرقم القومي")]
    [RegularExpression(@"^\d{14}$", ErrorMessage = "{0} لازم يكون 14 رقم")]
    public string? NationalId { get; set; }
}

public class QualificationDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }

    [Display(Name = "النوع")]
    [Required(ErrorMessage = "{0} مطلوب")]
    public QualificationType? Type { get; set; }

    [Display(Name = "المؤهل / الدورة")]
    [Required(ErrorMessage = "{0} مطلوب")]
    [StringLength(150, ErrorMessage = "{0} لازم يكون {1} حرف أو أقل")]
    public string Title { get; set; } = null!;

    [Display(Name = "الجهة")]
    [StringLength(150, ErrorMessage = "{0} لازم تكون {1} حرف أو أقل")]
    public string? Institution { get; set; }

    [Display(Name = "السنة")]
    [Range(1950, 2100, ErrorMessage = "{0} لازم تكون بين {1} و {2}")]
    public int? Year { get; set; }

    [Display(Name = "التقدير")]
    [StringLength(50, ErrorMessage = "{0} لازم يكون {1} حرف أو أقل")]
    public string? Grade { get; set; }
}

public class ExperienceDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }

    [Display(Name = "الشركة")]
    [Required(ErrorMessage = "{0} مطلوبة")]
    [StringLength(150, ErrorMessage = "{0} لازم تكون {1} حرف أو أقل")]
    public string Company { get; set; } = null!;

    [Display(Name = "المسمى")]
    [StringLength(100, ErrorMessage = "{0} لازم يكون {1} حرف أو أقل")]
    public string? JobTitle { get; set; }

    [Display(Name = "من")]
    public DateOnly? FromDate { get; set; }

    [Display(Name = "إلى")]
    public DateOnly? ToDate { get; set; }

    [Display(Name = "سبب الخروج")]
    [StringLength(250, ErrorMessage = "{0} لازم يكون {1} حرف أو أقل")]
    public string? LeavingReason { get; set; }
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
