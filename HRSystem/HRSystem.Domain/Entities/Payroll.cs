using HRSystem.Domain.Common;
using HRSystem.Domain.Enums;

namespace HRSystem.Domain.Entities;

public class SalaryComponent : BaseEntity
{
    public string Name { get; set; } = null!;
    public SalaryComponentType Type { get; set; }
    public CalculationMethod CalcMethod { get; set; }
    public bool IsTaxable { get; set; } = true;
    public bool IsInsurable { get; set; } = true;
}

public class EmployeeSalaryComponent : BaseEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public int SalaryComponentId { get; set; }
    public SalaryComponent SalaryComponent { get; set; } = null!;

    // Amount for Fixed components, percentage of basic salary for Percentage components
    public decimal Value { get; set; }
}

public class Loan : BaseEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public decimal Amount { get; set; }
    public int InstallmentsCount { get; set; }
    public decimal MonthlyInstallment { get; set; }
    public DateOnly StartMonth { get; set; }
    public decimal RemainingAmount { get; set; }
    public LoanStatus Status { get; set; } = LoanStatus.Active;
}

public class PayrollAdjustment : BaseEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public int Year { get; set; }
    public int Month { get; set; }
    public AdjustmentType Type { get; set; }
    public decimal? Amount { get; set; }
    public decimal? Days { get; set; }
    public string? Reason { get; set; }
}

public class PayrollRun : BaseEntity
{
    public int Year { get; set; }
    public int Month { get; set; }
    public PayrollRunStatus Status { get; set; } = PayrollRunStatus.Draft;
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public ICollection<PayrollItem> Items { get; set; } = new List<PayrollItem>();
}

public class PayrollItem : BaseEntity
{
    public int PayrollRunId { get; set; }
    public PayrollRun PayrollRun { get; set; } = null!;

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    // Earnings
    public decimal BasicSalary { get; set; }
    public decimal TotalAllowances { get; set; }
    public decimal OvertimeAmount { get; set; }
    public decimal Bonuses { get; set; }

    // Deductions
    public decimal AbsenceDeduction { get; set; }
    public decimal LateDeduction { get; set; }
    public decimal Penalties { get; set; }
    public decimal LoanInstallment { get; set; }
    public decimal OtherDeductions { get; set; }

    // Insurance and tax
    public decimal InsuranceEmployee { get; set; }
    public decimal InsuranceCompany { get; set; }
    public decimal IncomeTax { get; set; }

    public decimal GrossSalary { get; set; }
    public decimal NetSalary { get; set; }

    // What the amounts were based on, shown on the payslip
    public int SalaryDays { get; set; }
    public decimal AbsentDays { get; set; }
    public int LateMinutes { get; set; }
    public int OvertimeMinutes { get; set; }
}

public class TaxBracket : BaseEntity
{
    public int Year { get; set; }
    public decimal FromAmount { get; set; }
    public decimal? ToAmount { get; set; }
    public decimal Rate { get; set; }
}
