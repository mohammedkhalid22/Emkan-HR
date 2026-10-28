using HRSystem.Domain.Enums;

namespace HRSystem.BLL.Common;

public record PayrollComponent(SalaryComponentType Type, CalculationMethod Method, decimal Value, bool IsTaxable);

public record TaxBand(decimal From, decimal? To, decimal RatePercent);

// Company-wide numbers from SystemSettings / TaxBrackets
public record PayrollSettings(
    int DaysPerMonth,
    int HoursPerDay,
    decimal OvertimeRate,
    decimal InsuranceEmployeeRate,
    decimal InsuranceCompanyRate,
    decimal InsuranceMinSalary,     // 0 = no minimum
    decimal InsuranceMaxSalary,     // 0 = no maximum
    decimal AnnualTaxExemption,
    IReadOnlyList<TaxBand> TaxBands);

// One employee for one month
public record PayrollInput(
    decimal BasicSalary,
    decimal InsuranceSalary,
    int SalaryDays,                 // days employed this month (a full month = DaysPerMonth)
    IReadOnlyList<PayrollComponent> Components,
    decimal AbsentDays,             // absences + unpaid leave days
    int LateMinutes,                // late + early leave
    int OvertimeMinutes,
    decimal BonusAmount,
    decimal BonusDays,
    decimal PenaltyAmount,
    decimal PenaltyDays,
    decimal LoanInstallment);

public record PayrollResult(
    decimal BasicSalary,
    decimal TotalAllowances,
    decimal OvertimeAmount,
    decimal Bonuses,
    decimal AbsenceDeduction,
    decimal LateDeduction,
    decimal Penalties,
    decimal LoanInstallment,
    decimal OtherDeductions,
    decimal InsuranceEmployee,
    decimal InsuranceCompany,
    decimal IncomeTax,
    decimal GrossSalary,
    decimal NetSalary);

// Pure calculation (no database) so it can be unit tested. Rules: business rules section 8.4.
public static class PayrollCalculator
{
    public static PayrollResult Calculate(PayrollInput input, PayrollSettings settings)
    {
        // Someone who joined or left during the month is paid for the days they were employed
        var share = Math.Min(1m, (decimal)input.SalaryDays / settings.DaysPerMonth);

        var dayRate = input.BasicSalary / settings.DaysPerMonth;
        var hourRate = dayRate / settings.HoursPerDay;

        // Every amount is rounded first, so the payslip lines add up exactly to the totals
        var basic = Round(input.BasicSalary * share);

        decimal Amount(PayrollComponent c) => Round(c.Method == CalculationMethod.Percentage
            ? input.BasicSalary * c.Value / 100m * share
            : c.Value * share);

        var allowances = input.Components.Where(c => c.Type == SalaryComponentType.Allowance).ToList();
        var totalAllowances = allowances.Sum(Amount);
        var nonTaxableAllowances = allowances.Where(c => !c.IsTaxable).Sum(Amount);
        var otherDeductions = input.Components.Where(c => c.Type == SalaryComponentType.Deduction).Sum(Amount);

        var overtime = Round(input.OvertimeMinutes / 60m * hourRate * settings.OvertimeRate);
        var bonuses = Round(input.BonusAmount + input.BonusDays * dayRate);
        var penalties = Round(input.PenaltyAmount + input.PenaltyDays * dayRate);
        var absence = Round(input.AbsentDays * dayRate);
        var late = Round(input.LateMinutes / 60m * hourRate);
        var loan = Round(input.LoanInstallment);

        var gross = basic + totalAllowances + overtime + bonuses;

        var insurable = input.SalaryDays == 0 ? 0 : input.InsuranceSalary;
        if (settings.InsuranceMaxSalary > 0)
            insurable = Math.Min(insurable, settings.InsuranceMaxSalary);
        if (settings.InsuranceMinSalary > 0 && insurable > 0)
            insurable = Math.Max(insurable, settings.InsuranceMinSalary);

        var insuranceEmployee = Round(insurable * settings.InsuranceEmployeeRate);
        var insuranceCompany = Round(insurable * settings.InsuranceCompanyRate);

        // Monthly tax = tax on (taxable income × 12 − exemption) ÷ 12
        var taxableMonthly = Math.Max(0, gross - absence - late - penalties - insuranceEmployee - nonTaxableAllowances);
        var annualTaxable = Math.Max(0, taxableMonthly * 12 - settings.AnnualTaxExemption);
        var tax = Round(AnnualTax(annualTaxable, settings.TaxBands) / 12);

        var net = gross - insuranceEmployee - tax - absence - late - penalties - loan - otherDeductions;

        return new PayrollResult(
            basic, totalAllowances, overtime, bonuses,
            absence, late, penalties, loan, otherDeductions,
            insuranceEmployee, insuranceCompany, tax,
            gross, net);
    }

    // Each band taxes only the part of the income that falls inside it
    public static decimal AnnualTax(decimal annualIncome, IReadOnlyList<TaxBand> bands)
        => bands.OrderBy(b => b.From)
            .Sum(b => Math.Max(0, Math.Min(annualIncome, b.To ?? decimal.MaxValue) - b.From) * b.RatePercent / 100m);

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
