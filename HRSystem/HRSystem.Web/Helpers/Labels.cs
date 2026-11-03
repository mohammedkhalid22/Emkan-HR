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

    public static IEnumerable<SelectListItem> RoleOptions(string? selected = null)
        => Roles.All.Select(r => new SelectListItem(Role(r), r, r == selected));

    public static IEnumerable<SelectListItem> Options<TEnum>(Func<TEnum, string> label) where TEnum : struct, Enum
        => Enum.GetValues<TEnum>().Select(v => new SelectListItem(label(v), v.ToString()));
}
