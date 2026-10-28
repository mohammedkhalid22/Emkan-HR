using HRSystem.BLL.Common;
using HRSystem.Domain.Enums;

namespace HRSystem.Tests;

public class PayrollCalculatorTests
{
    private static readonly List<TaxBand> Bands2026 =
    [
        new(0, 40000, 0), new(40000, 55000, 10), new(55000, 70000, 15), new(70000, 200000, 20),
        new(200000, 400000, 22.5m), new(400000, 1200000, 25), new(1200000, null, 27.5m)
    ];

    private static readonly PayrollSettings Settings = new(
        DaysPerMonth: 30, HoursPerDay: 8, OvertimeRate: 1.35m,
        InsuranceEmployeeRate: 0.11m, InsuranceCompanyRate: 0.1875m,
        InsuranceMinSalary: 0, InsuranceMaxSalary: 0,
        AnnualTaxExemption: 20000, TaxBands: Bands2026);

    private static PayrollInput Input(decimal basic, int salaryDays = 30, decimal absentDays = 0, int lateMinutes = 0,
        int overtimeMinutes = 0, decimal loan = 0, List<PayrollComponent>? components = null, decimal? insurance = null)
        => new(basic, insurance ?? basic, salaryDays, components ?? [], absentDays, lateMinutes, overtimeMinutes, 0, 0, 0, 0, loan);

    [Fact]
    public void Full_month_with_tax_and_insurance()
    {
        var r = PayrollCalculator.Calculate(Input(30000), Settings);

        Assert.Equal(30000m, r.GrossSalary);
        Assert.Equal(3300m, r.InsuranceEmployee);       // 11%
        Assert.Equal(5625m, r.InsuranceCompany);        // 18.75%
        // (30000 − 3300) × 12 − 20000 = 300,400 a year
        // 1,500 + 2,250 + 26,000 + 22,590 = 52,340 a year → 4,361.67 a month
        Assert.Equal(4361.67m, r.IncomeTax);
        Assert.Equal(22338.33m, r.NetSalary);
    }

    [Fact]
    public void Income_below_the_zero_band_pays_no_tax()
    {
        // (4000 − 440) × 12 − 20000 = 22,720 → inside the 0% band
        Assert.Equal(0m, PayrollCalculator.Calculate(Input(4000), Settings).IncomeTax);
    }

    [Fact]
    public void Absent_days_cost_a_day_each()
        => Assert.Equal(600m, PayrollCalculator.Calculate(Input(9000, absentDays: 2), Settings).AbsenceDeduction);

    [Fact]
    public void Twenty_minutes_late()
    {
        // 9000 / 30 / 8 = 37.5 an hour → 20 minutes = 12.5
        Assert.Equal(12.5m, PayrollCalculator.Calculate(Input(9000, lateMinutes: 20), Settings).LateDeduction);
    }

    [Fact]
    public void Overtime_is_paid_at_the_overtime_rate()
    {
        // 30000 / 30 / 8 = 125 an hour → 45 minutes × 1.35 = 126.5625
        Assert.Equal(126.56m, PayrollCalculator.Calculate(Input(30000, overtimeMinutes: 45), Settings).OvertimeAmount);
    }

    [Fact]
    public void Half_month_gets_half_the_basic_and_allowances()
    {
        List<PayrollComponent> components = [new(SalaryComponentType.Allowance, CalculationMethod.Fixed, 1000, true)];
        var r = PayrollCalculator.Calculate(Input(9000, salaryDays: 15, components: components), Settings);

        Assert.Equal(4500m, r.BasicSalary);
        Assert.Equal(500m, r.TotalAllowances);
    }

    [Fact]
    public void Percentage_allowance_and_fixed_deduction()
    {
        List<PayrollComponent> components =
        [
            new(SalaryComponentType.Allowance, CalculationMethod.Percentage, 10, true),
            new(SalaryComponentType.Deduction, CalculationMethod.Fixed, 200, false)
        ];
        var r = PayrollCalculator.Calculate(Input(9000, components: components), Settings);

        Assert.Equal(900m, r.TotalAllowances);
        Assert.Equal(200m, r.OtherDeductions);
        Assert.Equal(9900m, r.GrossSalary);
    }

    [Fact]
    public void Loan_installment_reduces_net_only()
    {
        var without = PayrollCalculator.Calculate(Input(9000), Settings);
        var with = PayrollCalculator.Calculate(Input(9000, loan: 500), Settings);

        Assert.Equal(without.IncomeTax, with.IncomeTax);
        Assert.Equal(without.NetSalary - 500, with.NetSalary);
    }

    [Fact]
    public void Insurance_salary_is_capped_at_the_maximum()
    {
        var capped = Settings with { InsuranceMaxSalary = 14500 };
        Assert.Equal(1595m, PayrollCalculator.Calculate(Input(30000), capped).InsuranceEmployee);
    }

    [Fact]
    public void Insurance_salary_is_raised_to_the_minimum()
    {
        var floored = Settings with { InsuranceMinSalary = 2300 };
        Assert.Equal(253m, PayrollCalculator.Calculate(Input(2000), floored).InsuranceEmployee);
    }

    [Fact]
    public void Payslip_lines_add_up_exactly_to_net()
    {
        List<PayrollComponent> transport = [new(SalaryComponentType.Allowance, CalculationMethod.Fixed, 500, true)];
        var r = PayrollCalculator.Calculate(
            Input(11000, absentDays: 16, lateMinutes: 20, loan: 500, components: transport, insurance: 9000), Settings);

        var deductions = r.InsuranceEmployee + r.IncomeTax + r.AbsenceDeduction + r.LateDeduction
            + r.Penalties + r.LoanInstallment + r.OtherDeductions;

        Assert.Equal(4128.05m, r.NetSalary);
        Assert.Equal(r.GrossSalary - deductions, r.NetSalary);
        Assert.Equal(r.BasicSalary + r.TotalAllowances + r.OvertimeAmount + r.Bonuses, r.GrossSalary);
    }

    [Fact]
    public void Annual_tax_by_bands()
        => Assert.Equal(52340m, PayrollCalculator.AnnualTax(300400, Bands2026));
}
