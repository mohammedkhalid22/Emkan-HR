using HRSystem.BLL.DTOs;
using HRSystem.Domain.Enums;
using HRSystem.Domain.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRSystem.Web.Helpers;

// Arabic names for enums and roles, used in views, dropdowns and Excel
public static class Labels
{
    public static string Of(Gender value) => value switch
    {
        Gender.Male => "ذكر",
        Gender.Female => "أنثى",
        _ => value.ToString()
    };

    public static string Of(MaritalStatus value) => value switch
    {
        MaritalStatus.Single => "أعزب",
        MaritalStatus.Married => "متزوج",
        MaritalStatus.Divorced => "مطلق",
        MaritalStatus.Widowed => "أرمل",
        _ => value.ToString()
    };

    public static string Of(ContractType value) => value switch
    {
        ContractType.Permanent => "دائم",
        ContractType.Temporary => "مؤقت",
        ContractType.PartTime => "دوام جزئي",
        _ => value.ToString()
    };

    public static string Of(EmployeeStatus value) => value switch
    {
        EmployeeStatus.Active => "نشط",
        EmployeeStatus.Suspended => "موقوف",
        EmployeeStatus.Terminated => "منتهي الخدمة",
        _ => value.ToString()
    };

    public static string StatusBadge(EmployeeStatus value) => value switch
    {
        EmployeeStatus.Active => "text-bg-success",
        EmployeeStatus.Suspended => "text-bg-warning",
        _ => "text-bg-secondary"
    };

    public static string Of(CandidateStatus value) => value switch
    {
        CandidateStatus.New => "جديد",
        CandidateStatus.Interview => "مقابلة",
        CandidateStatus.Accepted => "مقبول",
        CandidateStatus.Rejected => "مرفوض",
        CandidateStatus.Hired => "اتعيّن",
        _ => value.ToString()
    };

    public static string StatusBadge(CandidateStatus value) => value switch
    {
        CandidateStatus.New => "text-bg-light",
        CandidateStatus.Interview => "text-bg-info",
        CandidateStatus.Accepted => "text-bg-success",
        CandidateStatus.Rejected => "text-bg-danger",
        _ => "text-bg-primary"
    };

    // Bootstrap icon and colour per kind of notification
    public static (string Icon, string Tone) NotificationIcon(NotificationType type) => type switch
    {
        NotificationType.RequestWaiting => ("bi-hourglass-split", "#2a78d6"),
        NotificationType.RequestApproved => ("bi-check-circle", "#0ca30c"),
        NotificationType.RequestRejected => ("bi-x-circle", "#d03b3b"),
        NotificationType.Payslip => ("bi-receipt", "#6250d6"),
        NotificationType.ContractEnding => ("bi-file-earmark-text", "#eb6834"),
        NotificationType.DocumentExpiring => ("bi-exclamation-triangle", "#eda100"),
        NotificationType.Birthday => ("bi-gift", "#e87ba4"),
        NotificationType.WorkAnniversary => ("bi-award", "#1baf7a"),
        _ => ("bi-bell", "#2a78d6")
    };

    // "من 5 دقايق", "إمبارح"... (times are stored in UTC)
    public static string Ago(DateTime utc)
    {
        var span = DateTime.UtcNow - utc;
        if (span.TotalMinutes < 1) return "دلوقتي";
        if (span.TotalMinutes < 60) return $"من {(int)span.TotalMinutes} دقيقة";
        if (span.TotalHours < 24) return $"من {(int)span.TotalHours} ساعة";
        if (span.TotalDays < 2) return "إمبارح";
        if (span.TotalDays < 7) return $"من {(int)span.TotalDays} أيام";
        return utc.ToLocalTime().ToString("yyyy-MM-dd");
    }

    // AI match score: green 75+, yellow 50+, red below
    public static string MatchTone(int percent) => percent >= 75 ? "#0ca30c" : percent >= 50 ? "#fab219" : "#d03b3b";

    public static string MatchVerdict(int percent) => percent >= 75 ? "مناسب جداً" : percent >= 50 ? "مناسب لحد ما" : "محتاج تطوير";

    public static string Of(RequestStatus value) => value switch
    {
        RequestStatus.Pending => "بانتظار المدير",
        RequestStatus.ManagerApproved => "بانتظار شؤون العاملين",
        RequestStatus.Approved => "معتمدة",
        RequestStatus.Rejected => "مرفوضة",
        RequestStatus.Cancelled => "ملغية",
        _ => value.ToString()
    };

    // Someone without a manager skips the first step, so their pending request is already with HR
    public static string Of(LeaveRequestDto request)
        => request.Status == RequestStatus.Pending && request.EmployeeManagerId is null
            ? Of(RequestStatus.ManagerApproved)
            : Of(request.Status);

    public static string StatusBadge(RequestStatus value) => value switch
    {
        RequestStatus.Pending => "text-bg-warning",
        RequestStatus.ManagerApproved => "text-bg-info",
        RequestStatus.Approved => "text-bg-success",
        RequestStatus.Rejected => "text-bg-danger",
        _ => "text-bg-secondary"
    };

    public static string Days(decimal value) => value.ToString("0.#");

    public static string Of(AttendanceStatus value) => value switch
    {
        AttendanceStatus.Present => "حاضر",
        AttendanceStatus.Absent => "غائب",
        AttendanceStatus.OnLeave => "إجازة",
        AttendanceStatus.Holiday => "عطلة",
        AttendanceStatus.Mission => "مأمورية",
        _ => value.ToString()
    };

    public static string StatusBadge(AttendanceStatus value) => value switch
    {
        AttendanceStatus.Present => "text-bg-success",
        AttendanceStatus.Absent => "text-bg-danger",
        AttendanceStatus.OnLeave => "text-bg-info",
        AttendanceStatus.Mission => "text-bg-primary",
        _ => "text-bg-secondary"
    };

    public static string Of(AttendanceSource value) => value switch
    {
        AttendanceSource.Manual => "يدوي",
        AttendanceSource.Device => "بصمة",
        AttendanceSource.Web => "من البرنامج",
        AttendanceSource.System => "تلقائي",
        _ => value.ToString()
    };

    public static string Of(AttendanceRequestType value) => value switch
    {
        AttendanceRequestType.Permission => "إذن",
        AttendanceRequestType.Mission => "مأمورية",
        _ => value.ToString()
    };

    public static string Of(AttendanceRequestDto request)
        => request.Status == RequestStatus.Pending && request.EmployeeManagerId is null
            ? Of(RequestStatus.ManagerApproved)
            : Of(request.Status);

    public static string Of(SalaryComponentType value) => value == SalaryComponentType.Allowance ? "بدل / استحقاق" : "استقطاع";

    public static string Of(CalculationMethod value) => value == CalculationMethod.Fixed ? "مبلغ ثابت" : "نسبة من الأساسي";

    public static string Of(AdjustmentType value) => value == AdjustmentType.Bonus ? "مكافأة" : "جزاء";

    public static string Of(LoanStatus value) => value == LoanStatus.Active ? "سارية" : "خلصت";

    public static string Of(PayrollRunStatus value) => value == PayrollRunStatus.Draft ? "مسودة" : "معتمد";

    public static string Money(decimal value) => value.ToString("N2");

    private static readonly string[] MonthNames =
        ["يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو", "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر"];

    public static string Month(int month) => month is >= 1 and <= 12 ? MonthNames[month - 1] : month.ToString();

    public static string Month(int year, int month) => $"{Month(month)} {year}";

    // Arabic names for the keys in SystemSettings
    public static string Setting(string key) => key switch
    {
        "WeekendDays" => "أيام الإجازة الأسبوعية (بالإنجليزي)",
        "SalaryDaysPerMonth" => "أيام الشهر في حساب الأجر",
        "WorkHoursPerDay" => "ساعات العمل في اليوم",
        "OvertimeRate" => "معامل الأوفر تايم",
        "OvertimeMinimumMinutes" => "أقل مدة أوفر تايم (دقايق)",
        "InsuranceEmployeeRate" => "حصة الموظف في التأمينات (0.11 = 11%)",
        "InsuranceCompanyRate" => "حصة الشركة في التأمينات (0.1875 = 18.75%)",
        "InsuranceMinSalary" => "الحد الأدنى لأجر الاشتراك (0 = بدون)",
        "InsuranceMaxSalary" => "الحد الأقصى لأجر الاشتراك (0 = بدون)",
        "TaxPersonalExemption" => "الإعفاء الشخصي السنوي من الضريبة",
        "RequireTwoFactorForAdmins" => "إلزام الأدمن والـ HR بالتحقق بخطوتين (1 = أيوه، 0 = لأ)",
        "RequireSelfie" => "سيلفي مع تسجيل الحضور والانصراف (1 = أيوه، 0 = لأ)",
        _ => key
    };

    // 80 → "1:20"
    public static string Minutes(int minutes) => minutes == 0 ? "—" : $"{minutes / 60}:{minutes % 60:D2}";

    public static string Time(TimeOnly? time) => time?.ToString("HH:mm") ?? "—";

    public static string Role(string? role) => role switch
    {
        Roles.Admin => "مدير النظام",
        Roles.HR => "شؤون العاملين",
        Roles.Manager => "مدير",
        Roles.Employee => "موظف",
        _ => "—"
    };

    // ---------- Audit log ----------

    public static string AuditEntity(string name) => name switch
    {
        "Branch" => "فرع",
        "Department" => "قسم",
        "JobTitle" => "مسمى وظيفي",
        "Employee" => "موظف",
        "EmployeeDocument" => "مستند موظف",
        "EmployeeHistory" => "تاريخ موظف",
        "CvAnalysis" => "تحليل CV",
        "Candidate" => "متقدم لوظيفة",
        "LeaveType" => "نوع إجازة",
        "LeaveBalance" => "رصيد إجازة",
        "LeaveRequest" => "طلب إجازة",
        "PublicHoliday" => "عطلة رسمية",
        "Shift" => "وردية",
        "AttendanceRecord" => "سجل حضور",
        "AttendanceRequest" => "إذن / مأمورية",
        "SalaryComponent" => "مكوّن مرتب",
        "EmployeeSalaryComponent" => "بدل / استقطاع موظف",
        "Loan" => "سلفة",
        "PayrollAdjustment" => "مكافأة / جزاء",
        "PayrollRun" => "مسير مرتبات",
        "PayrollItem" => "كشف مرتب",
        "TaxBracket" => "شريحة ضريبة",
        "SystemSetting" => "إعداد",
        "ApplicationUser" => "حساب دخول",
        "UserRole" => "صلاحية حساب",
        _ => name
    };

    public static (string Text, string Badge) AuditAction(string action) => action switch
    {
        "Insert" => ("إضافة", "text-bg-success"),
        "Update" => ("تعديل", "text-bg-primary"),
        "Delete" => ("حذف", "text-bg-danger"),
        _ => (action, "text-bg-secondary")
    };

    private static readonly Dictionary<string, string> AuditFields = new()
    {
        ["Name"] = "الاسم", ["FullName"] = "الاسم", ["FullNameAr"] = "الاسم", ["FullNameEn"] = "الاسم بالإنجليزي",
        ["Code"] = "الكود", ["NationalId"] = "الرقم القومي", ["BirthDate"] = "تاريخ الميلاد", ["Gender"] = "النوع",
        ["MaritalStatus"] = "الحالة الاجتماعية", ["Phone"] = "الموبايل", ["Email"] = "البريد", ["Address"] = "العنوان",
        ["PhotoPath"] = "الصورة", ["HireDate"] = "تاريخ التعيين", ["ContractType"] = "نوع العقد", ["ContractEndDate"] = "نهاية العقد",
        ["Status"] = "الحالة", ["TerminationDate"] = "تاريخ إنهاء الخدمة", ["TerminationReason"] = "سبب إنهاء الخدمة",
        ["BasicSalary"] = "المرتب الأساسي", ["InsuranceSalary"] = "أجر التأمينات", ["BankAccount"] = "الحساب البنكي",
        ["DepartmentId"] = "القسم", ["JobTitleId"] = "المسمى الوظيفي", ["ManagerId"] = "المدير", ["ShiftId"] = "الوردية",
        ["BranchId"] = "الفرع", ["ParentDepartmentId"] = "القسم الرئيسي", ["EmployeeId"] = "الموظف", ["LeaveTypeId"] = "نوع الإجازة",
        ["AllowRemoteAttendance"] = "حضور من أي مكان", ["UserId"] = "الحساب",
        ["StartDate"] = "من", ["EndDate"] = "إلى", ["Days"] = "الأيام", ["Reason"] = "السبب", ["RejectionReason"] = "سبب الرفض",
        ["Year"] = "السنة", ["Month"] = "الشهر", ["Entitled"] = "المستحق", ["Used"] = "المستخدم", ["Date"] = "التاريخ",
        ["CheckIn"] = "الحضور", ["CheckOut"] = "الانصراف", ["LateMinutes"] = "دقايق التأخير", ["EarlyLeaveMinutes"] = "انصراف بدري (دقايق)",
        ["OvertimeMinutes"] = "دقايق الأوفر تايم", ["Source"] = "المصدر", ["Notes"] = "ملاحظات", ["Type"] = "النوع",
        ["FromTime"] = "من الساعة", ["ToTime"] = "للساعة", ["StartTime"] = "بداية الوردية", ["EndTime"] = "نهاية الوردية", ["GraceMinutes"] = "دقايق السماح",
        ["Amount"] = "المبلغ", ["Value"] = "القيمة", ["InstallmentsCount"] = "عدد الأقساط", ["MonthlyInstallment"] = "القسط الشهري",
        ["RemainingAmount"] = "المتبقي", ["NetSalary"] = "الصافي", ["GrossSalary"] = "الإجمالي", ["Rate"] = "النسبة",
        ["AnnualDays"] = "أيام في السنة", ["IsPaid"] = "مدفوعة", ["MinSalary"] = "أقل مرتب", ["MaxSalary"] = "أعلى مرتب",
        ["Latitude"] = "خط العرض", ["Longitude"] = "خط الطول", ["AttendanceRadiusMeters"] = "نطاق الحضور (متر)",
        ["IsActive"] = "مفعّل", ["TwoFactorEnabled"] = "التحقق بخطوتين", ["LockoutEnd"] = "مقفول لحد", ["PasswordHash"] = "كلمة السر",
        ["RoleId"] = "الدور", ["IsDeleted"] = "محذوف", ["ApprovedAt"] = "اتعتمد في", ["ApprovedBy"] = "اعتمده",
        ["ManagerActionBy"] = "قرار المدير من", ["ManagerActionAt"] = "قرار المدير في", ["HrActionBy"] = "قرار شؤون العاملين من",
        ["HrActionAt"] = "قرار شؤون العاملين في", ["AttachmentPath"] = "المرفق", ["DocumentType"] = "نوع المستند",
        ["FilePath"] = "الملف", ["ExpiryDate"] = "تاريخ الانتهاء", ["HiredEmployeeId"] = "اتعيّن كموظف رقم", ["JobTitleName"] = "الوظيفة",
        ["MatchPercent"] = "نسبة التوافق", ["CvPath"] = "ملف الـ CV"
    };

    public static string AuditField(string field) => AuditFields.GetValueOrDefault(field, field);

    // Values come typed from AuditLogService; enums reuse the Arabic names above
    public static string AuditValue(string field, object? value) => value switch
    {
        null => "—",
        _ when field == "PasswordHash" => "اتغيرت",
        _ when field == "RoleId" && value is string role => Role(role),
        bool b => b ? "أيوه" : "لأ",
        Enum e => EnumName(e),
        DateTime d => d.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
        DateTimeOffset d => d.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
        DateOnly d => d.ToString("yyyy-MM-dd"),
        TimeOnly t => t.ToString("HH:mm"),
        decimal m => m.ToString("0.##"),
        string s when s.Length > 80 => s[..80] + "…",
        _ => value.ToString() ?? "—"
    };

    private static string EnumName(Enum value)
    {
        try { return Of((dynamic)value); }
        catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { return value.ToString(); }
    }

    public static IEnumerable<SelectListItem> RoleOptions(string? selected = null)
        => Roles.All.Select(r => new SelectListItem(Role(r), r, r == selected));

    public static IEnumerable<SelectListItem> Options<TEnum>(Func<TEnum, string> label) where TEnum : struct, Enum
        => Enum.GetValues<TEnum>().Select(v => new SelectListItem(label(v), v.ToString()));
}
