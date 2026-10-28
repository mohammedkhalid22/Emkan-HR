using System.ComponentModel.DataAnnotations;
using HRSystem.Domain.Enums;

namespace HRSystem.BLL.DTOs;

// ---------- Salary components ----------

public class SalaryComponentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public SalaryComponentType Type { get; set; }
    public CalculationMethod CalcMethod { get; set; }
    public bool IsTaxable { get; set; }
    public int EmployeesCount { get; set; }
}

public class SaveSalaryComponentDto
{
    public int Id { get; set; }

    [Display(Name = "اسم المكوّن")]
    [Required(ErrorMessage = "{0} مطلوب")]
    [StringLength(100, ErrorMessage = "{0} لازم يكون {1} حرف أو أقل")]
    public string Name { get; set; } = null!;

    [Display(Name = "النوع")]
    [Required(ErrorMessage = "{0} مطلوب")]
    public SalaryComponentType? Type { get; set; }

    [Display(Name = "طريقة الحساب")]
    [Required(ErrorMessage = "{0} مطلوبة")]
    public CalculationMethod? CalcMethod { get; set; }

    [Display(Name = "خاضع للضريبة")]
    public bool IsTaxable { get; set; } = true;
}

public class EmployeeComponentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public SalaryComponentType Type { get; set; }
    public CalculationMethod CalcMethod { get; set; }
    public decimal Value { get; set; }
}

// ---------- Loans ----------

public class LoanDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = null!;
    public decimal Amount { get; set; }
    public int InstallmentsCount { get; set; }
    public decimal MonthlyInstallment { get; set; }
    public DateOnly StartMonth { get; set; }
    public decimal RemainingAmount { get; set; }
    public LoanStatus Status { get; set; }
    public bool CanCancel => Status == LoanStatus.Active && RemainingAmount == Amount;
}

public class CreateLoanDto
{
    [Display(Name = "الموظف")]
    [Required(ErrorMessage = "{0} مطلوب")]
    public int? EmployeeId { get; set; }

    [Display(Name = "مبلغ السلفة")]
    [Required(ErrorMessage = "{0} مطلوب")]
    [Range(1, 10_000_000, ErrorMessage = "{0} لازم يكون بين {1} و {2}")]
    public decimal? Amount { get; set; }

    [Display(Name = "عدد الأقساط")]
    [Required(ErrorMessage = "{0} مطلوب")]
    [Range(1, 60, ErrorMessage = "{0} لازم يكون بين {1} و {2}")]
    public int? InstallmentsCount { get; set; }

    [Display(Name = "أول شهر خصم")]
    [Required(ErrorMessage = "{0} مطلوب")]
    public DateOnly? StartMonth { get; set; }
}

// ---------- Bonuses / penalties ----------

public class AdjustmentDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = null!;
    public int Year { get; set; }
    public int Month { get; set; }
    public AdjustmentType Type { get; set; }
    public decimal? Amount { get; set; }
    public decimal? Days { get; set; }
    public string? Reason { get; set; }
}

public class CreateAdjustmentDto
{
    [Display(Name = "الموظف")]
    [Required(ErrorMessage = "{0} مطلوب")]
    public int? EmployeeId { get; set; }

    [Display(Name = "النوع")]
    [Required(ErrorMessage = "{0} مطلوب")]
    public AdjustmentType? Type { get; set; }

    [Display(Name = "السنة")]
    public int Year { get; set; }

    [Display(Name = "الشهر")]
    [Range(1, 12)]
    public int Month { get; set; }

    [Display(Name = "المبلغ")]
    [Range(0, 10_000_000, ErrorMessage = "{0} لازم يكون بين {1} و {2}")]
    public decimal? Amount { get; set; }

    [Display(Name = "أو عدد أيام")]
    [Range(0, 30, ErrorMessage = "{0} لازم يكون بين {1} و {2}")]
    public decimal? Days { get; set; }

    [Display(Name = "السبب")]
    [Required(ErrorMessage = "{0} مطلوب")]
    [StringLength(500)]
    public string Reason { get; set; } = null!;
}

// ---------- Payroll runs ----------

public class PayrollRunDto
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public PayrollRunStatus Status { get; set; }
    public int EmployeesCount { get; set; }
    public decimal TotalGross { get; set; }
    public decimal TotalNet { get; set; }
    public decimal TotalInsuranceCompany { get; set; }
    public DateTime? ApprovedAt { get; set; }
}

public class PayrollItemDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public string DepartmentName { get; set; } = null!;
    public string JobTitleName { get; set; } = null!;
    public string? BankAccount { get; set; }

    public int SalaryDays { get; set; }
    public decimal AbsentDays { get; set; }
    public int LateMinutes { get; set; }
    public int OvertimeMinutes { get; set; }

    public decimal BasicSalary { get; set; }
    public decimal TotalAllowances { get; set; }
    public decimal OvertimeAmount { get; set; }
    public decimal Bonuses { get; set; }
    public decimal AbsenceDeduction { get; set; }
    public decimal LateDeduction { get; set; }
    public decimal Penalties { get; set; }
    public decimal LoanInstallment { get; set; }
    public decimal OtherDeductions { get; set; }
    public decimal InsuranceEmployee { get; set; }
    public decimal InsuranceCompany { get; set; }
    public decimal IncomeTax { get; set; }
    public decimal GrossSalary { get; set; }
    public decimal NetSalary { get; set; }

    public decimal TotalDeductions => AbsenceDeduction + LateDeduction + Penalties + LoanInstallment
        + OtherDeductions + InsuranceEmployee + IncomeTax;
}

public class PayrollRunDetailsDto
{
    public PayrollRunDto Run { get; set; } = null!;
    public List<PayrollItemDto> Items { get; set; } = [];
}

public class PayslipDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public PayrollRunStatus RunStatus { get; set; }
    public PayrollItemDto Item { get; set; } = null!;
}

public class MyPayslipDto
{
    public int ItemId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal GrossSalary { get; set; }
    public decimal NetSalary { get; set; }
}

// ---------- Settings ----------

public class SettingDto
{
    public string Key { get; set; } = null!;
    public string Value { get; set; } = null!;
    public string? Description { get; set; }
}

public class TaxBracketDto
{
    public int Id { get; set; }
    public int Year { get; set; }
    public decimal FromAmount { get; set; }
    public decimal? ToAmount { get; set; }
    public decimal Rate { get; set; }
}

public class SaveTaxBracketDto
{
    public int Year { get; set; }

    [Display(Name = "من (سنوي)")]
    [Required(ErrorMessage = "{0} مطلوب")]
    [Range(0, 1_000_000_000)]
    public decimal? FromAmount { get; set; }

    [Display(Name = "إلى (سنوي)")]
    [Range(0, 1_000_000_000)]
    public decimal? ToAmount { get; set; }

    [Display(Name = "النسبة %")]
    [Required(ErrorMessage = "{0} مطلوبة")]
    [Range(0, 100)]
    public decimal? Rate { get; set; }
}
