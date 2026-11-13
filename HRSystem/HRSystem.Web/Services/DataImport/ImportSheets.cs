using HRSystem.Domain.Enums;
using HRSystem.Web.Helpers;

namespace HRSystem.Web.Services.DataImport;

// One column of a sheet: its header in the file, whether it must be filled, a hint and (when it is a
// closed set) the allowed words, which become a dropdown in the template
public record ImportColumn(string Key, string Header, bool Required = false, string? Hint = null, string[]? Options = null);

// One sheet = one screen of basic data. Sheets are saved in this order, so what a row points at
// (a branch, a department, a job title...) exists before the row that needs it.
public record ImportSheet(string Key, string Name, string Description, ImportColumn[] Columns)
{
    public ImportColumn Column(string key) => Columns.First(c => c.Key == key);
}

public static class ImportSheets
{
    private static readonly string[] YesNo = ["أيوه", "لأ"];

    private static string[] Labels<T>(Func<T, string> label) where T : struct, Enum
        => Enum.GetValues<T>().Select(label).Distinct().ToArray();

    public static readonly ImportSheet Branches = new("branches", "الفروع", "فروع الشركة ومكان كل فرع لتسجيل الحضور من الموبايل",
    [
        new("name", "اسم الفرع", true),
        new("address", "العنوان"),
        new("lat", "خط العرض", Hint: "من Google Maps، زي 30.0444"),
        new("lng", "خط الطول", Hint: "زي 31.2357"),
        new("radius", "نطاق الحضور (متر)", Hint: "فاضي = 200"),
    ]);

    public static readonly ImportSheet Departments = new("departments", "الإدارات والأقسام", "الإدارة أعلى مستوى، والقسم تابع لإدارة",
    [
        new("name", "الاسم", true),
        new("type", "النوع", true, Options: Labels<DepartmentType>(Helpers.Labels.Of)),
        new("parent", "تابع لـ", Hint: "اسم الإدارة اللي القسم تابع ليها (لازم للقسم)"),
        new("branch", "الفرع", Hint: "اسم الفرع زي ما هو في شيت الفروع"),
    ]);

    public static readonly ImportSheet JobTitles = new("jobtitles", "المسميات الوظيفية", "المسميات ونطاق المرتب ومتطلبات كل وظيفة",
    [
        new("name", "المسمى الوظيفي", true),
        new("min", "الحد الأدنى للمرتب"),
        new("max", "الحد الأقصى للمرتب"),
        new("requirements", "متطلبات الوظيفة", Hint: "الذكاء الاصطناعي بيقارن الـ CVs بيها"),
    ]);

    public static readonly ImportSheet Shifts = new("shifts", "الورديات", "مواعيد العمل",
    [
        new("name", "اسم الوردية", true),
        new("start", "البداية", true, "زي 09:00"),
        new("end", "النهاية", true, "زي 17:00"),
        new("grace", "دقايق السماح", Hint: "فاضي = 15"),
    ]);

    public static readonly ImportSheet Positions = new("positions", "المواقع الوظيفية", "كراسي الهيكل: كل موقع في قسم وتابع لموقع أعلى",
    [
        new("name", "اسم الموقع", true),
        new("department", "الإدارة / القسم", true),
        new("jobtitle", "المسمى الوظيفي", true),
        new("parent", "تابع لـ (الموقع الأعلى)", Hint: "اسم الموقع الأعلى. لو الاسم متكرر في أكتر من قسم اكتبه: الاسم / القسم"),
        new("capacity", "عدد الشاغلين", Hint: "فاضي = 1"),
    ]);

    public static readonly ImportSheet LeaveTypes = new("leavetypes", "أنواع الإجازات", "أنواع الإجازات وأرصدتها السنوية",
    [
        new("name", "اسم النوع", true),
        new("days", "الرصيد السنوي (أيام)", true),
        new("senior", "رصيد الأقدمية", Hint: "لـ 10 سنين خدمة أو سن 50"),
        new("paid", "مدفوعة الأجر", Options: YesNo),
        new("attachment", "محتاجة مرفق", Options: YesNo),
    ]);

    public static readonly ImportSheet Holidays = new("holidays", "العطلات الرسمية", "أيام العطلات اللي مش بتتحسب غياب ولا من الإجازة",
    [
        new("date", "التاريخ", true, "زي 2026-10-06"),
        new("name", "اسم العطلة", true),
    ]);

    public static readonly ImportSheet SalaryComponents = new("components", "مكونات المرتب", "البدلات والاستقطاعات الثابتة",
    [
        new("name", "اسم المكوّن", true),
        new("type", "النوع", true, Options: Labels<SalaryComponentType>(Helpers.Labels.Of)),
        new("method", "طريقة الحساب", true, Options: Labels<CalculationMethod>(Helpers.Labels.Of)),
        new("taxable", "خاضع للضريبة", Options: YesNo),
    ]);

    public static readonly ImportSheet Employees = new("employees", "الموظفين", "ملفات الموظفين. الموظف بيتعرف بالرقم القومي: لو موجود بيتحدّث",
    [
        new("nationalid", "الرقم القومي", true, "14 رقم. تاريخ الميلاد والنوع بيتاخدوا منه"),
        new("namear", "الاسم بالعربي", true),
        new("nameen", "الاسم بالإنجليزي"),
        new("phone", "الموبايل", Hint: "01xxxxxxxxx"),
        new("email", "البريد الإلكتروني"),
        new("address", "العنوان"),
        new("marital", "الحالة الاجتماعية", Options: Labels<MaritalStatus>(Helpers.Labels.Of)),
        new("hiredate", "تاريخ التعيين", true),
        new("contract", "نوع العقد", true, Options: Labels<ContractType>(Helpers.Labels.Of)),
        new("contractend", "نهاية العقد", Hint: "لازم للعقد المؤقت"),
        new("position", "الموقع الوظيفي", Hint: "لو اتكتب، القسم والمسمى والمدير بيتاخدوا منه"),
        new("department", "الإدارة / القسم", Hint: "مطلوب لو مفيش موقع وظيفي"),
        new("jobtitle", "المسمى الوظيفي", Hint: "مطلوب لو مفيش موقع وظيفي"),
        new("manager", "المدير المباشر (رقمه القومي)", Hint: "لو مفيش موقع وظيفي"),
        new("shift", "الوردية"),
        new("salary", "المرتب الأساسي", true),
        new("insurance", "أجر الاشتراك التأميني", Hint: "فاضي = نفس المرتب"),
        new("bank", "البنك"),
        new("bankaccount", "رقم الحساب البنكي"),
        new("iban", "IBAN"),
        new("fingerprint", "رقم البصمة"),
        new("remote", "حضور من أي مكان", Options: YesNo),
        new("nationality", "الجنسية"),
        new("religion", "الديانة", Options: Labels<Religion>(Helpers.Labels.Of)),
        new("military", "الموقف من التجنيد", Options: Labels<MilitaryStatus>(Helpers.Labels.Of)),
        new("insuranceno", "الرقم التأميني"),
        new("governorate", "المحافظة"),
        new("city", "المدينة / الحي"),
        new("emergencyname", "اسم شخص للطوارئ"),
        new("emergencyphone", "موبايل الطوارئ"),
    ]);

    // The save order: whatever a sheet points at comes earlier
    public static readonly ImportSheet[] All = [Branches, Departments, JobTitles, Shifts, Positions, LeaveTypes, Holidays, SalaryComponents, Employees];

    public static ImportSheet? Find(string key) => All.FirstOrDefault(s => s.Key == key);
}
