using System.ComponentModel.DataAnnotations;

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
    public string? ParentDepartmentName { get; set; }
    public string? BranchName { get; set; }
    public string? ManagerName { get; set; }
    public int EmployeesCount { get; set; }
    public int SubDepartmentsCount { get; set; }
}

public class SaveDepartmentDto
{
    public int Id { get; set; }

    [Display(Name = "اسم القسم")]
    [Required(ErrorMessage = "{0} مطلوب")]
    [StringLength(100, ErrorMessage = "{0} لازم يكون {1} حرف أو أقل")]
    public string Name { get; set; } = null!;

    [Display(Name = "القسم الأب")]
    public int? ParentDepartmentId { get; set; }

    [Display(Name = "الفرع")]
    public int? BranchId { get; set; }

    [Display(Name = "مدير القسم")]
    public int? ManagerId { get; set; }
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
