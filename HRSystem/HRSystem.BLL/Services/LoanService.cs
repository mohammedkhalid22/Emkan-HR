using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.DAL.Repositories;
using HRSystem.Domain.Entities;
using HRSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.BLL.Services;

public interface ILoanService
{
    Task<List<LoanDto>> GetAllAsync(bool activeOnly);
    Task<List<LoanDto>> GetForEmployeeAsync(int employeeId);
    Task<ServiceResult> CreateAsync(CreateLoanDto dto);
    Task<ServiceResult> CancelAsync(int loanId);

    // Payroll: how much to take this month, and paying it once the month is approved
    Task<decimal> InstallmentDueAsync(int employeeId, int year, int month);
    Task PayInstallmentAsync(int employeeId, int year, int month, decimal amount);
}

public class LoanService : ILoanService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericRepository<Loan> _loans;

    public LoanService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
        _loans = unitOfWork.Repository<Loan>();
    }

    public Task<List<LoanDto>> GetAllAsync(bool activeOnly)
        => Project(_loans.QueryNoTracking().Where(l => !activeOnly || l.Status == LoanStatus.Active))
            .OrderByDescending(l => l.StartMonth)
            .ToListAsync();

    public Task<List<LoanDto>> GetForEmployeeAsync(int employeeId)
        => Project(_loans.QueryNoTracking().Where(l => l.EmployeeId == employeeId))
            .OrderByDescending(l => l.StartMonth)
            .ToListAsync();

    private static IQueryable<LoanDto> Project(IQueryable<Loan> query)
        => query.Select(l => new LoanDto
        {
            Id = l.Id,
            EmployeeId = l.EmployeeId,
            EmployeeName = l.Employee.FullNameAr,
            Amount = l.Amount,
            InstallmentsCount = l.InstallmentsCount,
            MonthlyInstallment = l.MonthlyInstallment,
            StartMonth = l.StartMonth,
            RemainingAmount = l.RemainingAmount,
            Status = l.Status
        });

    public async Task<ServiceResult> CreateAsync(CreateLoanDto dto)
    {
        var employee = await _unitOfWork.Repository<Employee>().QueryNoTracking().FirstOrDefaultAsync(e => e.Id == dto.EmployeeId);
        if (employee is null || employee.Status != EmployeeStatus.Active)
            return ServiceResult.Failure("السلفة لموظف نشط بس", nameof(dto.EmployeeId));

        // BR-35: one active loan at a time
        if (await _loans.QueryNoTracking().AnyAsync(l => l.EmployeeId == dto.EmployeeId && l.Status == LoanStatus.Active))
            return ServiceResult.Failure("الموظف عنده سلفة لسه ماخلصتش", nameof(dto.EmployeeId));

        var start = new DateOnly(dto.StartMonth!.Value.Year, dto.StartMonth.Value.Month, 1);
        if (await MonthApprovedAsync(start.Year, start.Month))
            return ServiceResult.Failure("مرتبات الشهر ده اتعتمدت. اختار شهر بعده", nameof(dto.StartMonth));

        var amount = dto.Amount!.Value;
        await _loans.AddAsync(new Loan
        {
            EmployeeId = dto.EmployeeId!.Value,
            Amount = amount,
            InstallmentsCount = dto.InstallmentsCount!.Value,
            MonthlyInstallment = Math.Round(amount / dto.InstallmentsCount.Value, 2),
            StartMonth = start,
            RemainingAmount = amount,
            Status = LoanStatus.Active
        });
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> CancelAsync(int loanId)
    {
        var loan = await _loans.GetByIdAsync(loanId);
        if (loan is null)
            return ServiceResult.Failure("السلفة مش موجودة");
        if (loan.RemainingAmount != loan.Amount)
            return ServiceResult.Failure("مينفعش تلغي سلفة اتخصم منها أقساط");

        _loans.Remove(loan);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<decimal> InstallmentDueAsync(int employeeId, int year, int month)
    {
        var monthStart = new DateOnly(year, month, 1);
        var loans = await _loans.QueryNoTracking()
            .Where(l => l.EmployeeId == employeeId && l.Status == LoanStatus.Active && l.StartMonth <= monthStart)
            .ToListAsync();

        // The last installment takes whatever is left (rounding)
        return loans.Sum(l => Math.Min(l.MonthlyInstallment, l.RemainingAmount));
    }

    public async Task PayInstallmentAsync(int employeeId, int year, int month, decimal amount)
    {
        var monthStart = new DateOnly(year, month, 1);
        var loans = await _loans.Query()
            .Where(l => l.EmployeeId == employeeId && l.Status == LoanStatus.Active && l.StartMonth <= monthStart)
            .OrderBy(l => l.StartMonth)
            .ToListAsync();

        foreach (var loan in loans)
        {
            var paid = Math.Min(amount, Math.Min(loan.MonthlyInstallment, loan.RemainingAmount));
            loan.RemainingAmount -= paid;
            amount -= paid;
            if (loan.RemainingAmount <= 0)
                loan.Status = LoanStatus.Closed;
        }
    }

    private Task<bool> MonthApprovedAsync(int year, int month)
        => _unitOfWork.Repository<PayrollRun>().QueryNoTracking()
            .AnyAsync(r => r.Year == year && r.Month == month && r.Status == PayrollRunStatus.Approved);
}
