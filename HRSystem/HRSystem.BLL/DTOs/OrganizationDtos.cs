using System.ComponentModel.DataAnnotations;
using HRSystem.BLL.Common;
using HRSystem.Domain.Enums;

namespace HRSystem.BLL.DTOs;

// ---------- Branches ----------

public class BranchDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Address { get; set; }
    public int DepartmentsCount { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int AttendanceRadiusMeters { get; set; }
}

public class SaveBranchDto
{
    public int Id { get; set; }

    [Display(Name = "اسم الفرع")]
    [Required(ErrorMessage = "{0} مطلوب")]
    [StringLength(100, ErrorMessage = "{0} لازم يكون {1} حرف أو أقل")]
    public string Name { get; set; } = null!;

    [Display(Name = "العنوان")]
    [StringLength(250, ErrorMessage = "{0} لازم يكون {1} حرف أو أقل")]
    public string? Address { get; set; }

    [Display(Name = "خط العرض (Latitude)")]
    [Range(-90, 90, ErrorMessage = "{0} لازم يكون بين {1} و {2}")]
    public double? Latitude { get; set; }

    [Display(Name = "خط الطول (Longitude)")]
    [Range(-180, 180, ErrorMessage = "{0} لازم يكون بين {1} و {2}")]
    public double? Longitude { get; set; }

    [Display(Name = "المسافة المسموحة للحضور (متر)")]
    [Range(30, 5000, ErrorMessage = "{0} لازم تكون بين {1} و {2} متر")]
    public int AttendanceRadiusMeters { get; set; } = 200;
}

// ---------- Departments ----------

public class DepartmentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public DepartmentType Type { get; set; }
    public string? ParentDepartmentName { get; set; }
    public string? BranchName { get; set; }
    public string? ManagerName { get; set; }
    public int EmployeesCount { get; set; }
    public int SubDepartmentsCount { get; set; }
}

public class SaveDepartmentDto
{
    public int Id { get; set; }

    [Display(Name = "الاسم")]
    [Required(ErrorMessage = "{0} مطلوب")]
    [StringLength(100, ErrorMessage = "{0} لازم يكون {1} حرف أو أقل")]
    public string Name { get; set; } = null!;

    [Display(Name = "النوع")]
    [Required(ErrorMessage = "{0} مطلوب")]
    public DepartmentType? Type { get; set; } = DepartmentType.Section;

    [Display(Name = "تابع لـ")]
    public int? ParentDepartmentId { get; set; }

    [Display(Name = "الفرع")]
    public int? BranchId { get; set; }

    [Display(Name = "مدير القسم")]
    public int? ManagerId { get; set; }
}

// ---------- Positions (seats in the org chart) ----------

public class PositionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = null!;
    public string JobTitleName { get; set; } = null!;
    public int? ParentPositionId { get; set; }
    public string? ParentPositionName { get; set; }
    public int Capacity { get; set; }
    public List<LookupDto> Holders { get; set; } = [];
}

public class SavePositionDto
{
    public int Id { get; set; }

    [Display(Name = "اسم الموقع الوظيفي")]
    [Required(ErrorMessage = "{0} مطلوب")]
    [StringLength(150, ErrorMessage = "{0} لازم يكون {1} حرف أو أقل")]
    public string Name { get; set; } = null!;

    [Display(Name = "الإدارة / القسم")]
    [Required(ErrorMessage = "{0} مطلوب")]
    public int? DepartmentId { get; set; }

    [Display(Name = "المسمى الوظيفي")]
    [Required(ErrorMessage = "{0} مطلوب")]
    public int? JobTitleId { get; set; }

    [Display(Name = "تابع لـ (الوظيفة الأعلى)")]
    public int? ParentPositionId { get; set; }

    [Display(Name = "عدد الشاغلين المسموح")]
    [Range(1, 100, ErrorMessage = "{0} لازم يكون بين {1} و {2}")]
    public int Capacity { get; set; } = 1;
}

// For the employee form: picking a position fills the department and job title
public record PositionOptionDto(int Id, string Name, int DepartmentId, string DepartmentName, int JobTitleId, int Holders, int Capacity);

// One unit of the org tree with its positions, each position with the ones under it
public class OrgUnitNode
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public DepartmentType Type { get; set; }
    public string? BranchName { get; set; }
    public int EmployeesCount { get; set; }
    public List<OrgPositionNode> Positions { get; set; } = [];
    public List<OrgUnitNode> Children { get; set; } = [];
}

public class OrgPositionNode
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string JobTitleName { get; set; } = null!;
    public int Capacity { get; set; }
    public List<LookupDto> Holders { get; set; } = [];

    // Set when the position reports to a position in another unit
    public string? ReportsToElsewhere { get; set; }
    public List<OrgPositionNode> Children { get; set; } = [];
}

// ---------- Job titles ----------

public class JobTitleDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public decimal? MinSalary { get; set; }
    public decimal? MaxSalary { get; set; }
    public int EmployeesCount { get; set; }
    public bool HasRequirements { get; set; }
}

public class SaveJobTitleDto
{
    public int Id { get; set; }

    [Display(Name = "المسمى الوظيفي")]
    [Required(ErrorMessage = "{0} مطلوب")]
    [StringLength(100, ErrorMessage = "{0} لازم يكون {1} حرف أو أقل")]
    public string Name { get; set; } = null!;

    [Display(Name = "الحد الأدنى للمرتب")]
    [Range(0, 10_000_000, ErrorMessage = "{0} لازم يكون بين {1} و {2}")]
    public decimal? MinSalary { get; set; }

    [Display(Name = "الحد الأقصى للمرتب")]
    [Range(0, 10_000_000, ErrorMessage = "{0} لازم يكون بين {1} و {2}")]
    public decimal? MaxSalary { get; set; }

    [Display(Name = "متطلبات الوظيفة")]
    [StringLength(2000, ErrorMessage = "{0} لازم تكون {1} حرف أو أقل")]
    public string? Requirements { get; set; }
}
