namespace HRSystem.Domain.Enums;

public enum SalaryComponentType : byte
{
    Allowance = 1,
    Deduction = 2
}

public enum CalculationMethod : byte
{
    Fixed = 1,
    Percentage = 2
}

public enum LoanStatus : byte
{
    Active = 1,
    Closed = 2
}

public enum AdjustmentType : byte
{
    Bonus = 1,
    Penalty = 2
}

public enum PayrollRunStatus : byte
{
    Draft = 1,
    Approved = 2
}
