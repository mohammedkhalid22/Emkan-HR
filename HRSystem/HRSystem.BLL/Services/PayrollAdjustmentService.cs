using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.DAL.Repositories;
using HRSystem.Domain.Entities;
using HRSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.BLL.Services;

// Bonuses and penalties for one month
public interface IPayrollAdjustmentService
{
    Task<List<AdjustmentDto>> GetForMonthAsync(int year, int month);
    Task<ServiceResult> CreateAsync(CreateAdjustmentDto dto);
    Task<ServiceResult> DeleteAsync(int id);
}

public class PayrollAdjustmentService : IPayrollAdjustmentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericRepository<PayrollAdjustment> _adjustments;

    public PayrollAdjustmentService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
        _adjustments = unitOfWork.Repository<PayrollAdjustment>();
    }

    public Task<List<AdjustmentDto>> GetForMonthAsync(int year, int month)
        => _adjustments.QueryNoTracking()
            .Where(a => a.Year == year && a.Month == month)
            .OrderBy(a => a.Employee.Code)
            .Select(a => new AdjustmentDto
            {
                Id = a.Id,
                EmployeeId = a.EmployeeId,
                EmployeeName = a.Employee.FullNameAr,
                Year = a.Year,
                Month = a.Month,
                Type = a.Type,
                Amount = a.Amount,
                Days = a.Days,
                Reason = a.Reason
            })
            .ToListAsync();

    public async Task<ServiceResult> CreateAsync(CreateAdjustmentDto dto)
    {
        if ((dto.Amount ?? 0) <= 0 && (dto.Days ?? 0) <= 0)
            return ServiceResult.Failure("اكتب مبلغ أو عدد أيام");
        if (dto.Amount > 0 && dto.Days > 0)
            return ServiceResult.Failure("اكتب مبلغ أو عدد أيام، مش الاتنين");
        if (await MonthApprovedAsync(dto.Year, dto.Month))
            return ServiceResult.Failure("مرتبات الشهر ده اتعتمدت. ضيفها على الشهر اللي بعده");
        if (!await _unitOfWork.Repository<Employee>().QueryNoTracking().AnyAsync(e => e.Id == dto.EmployeeId))
            return ServiceResult.Failure("الموظف مش موجود");

        await _adjustments.AddAsync(new PayrollAdjustment
        {
            EmployeeId = dto.EmployeeId!.Value,
            Year = dto.Year,
            Month = dto.Month,
            Type = dto.Type!.Value,
            Amount = dto.Amount > 0 ? dto.Amount : null,
            Days = dto.Days > 0 ? dto.Days : null,
            Reason = dto.Reason.Trim()
        });
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var adjustment = await _adjustments.GetByIdAsync(id);
        if (adjustment is null)
            return ServiceResult.Failure("مش موجود");
        if (await MonthApprovedAsync(adjustment.Year, adjustment.Month))
            return ServiceResult.Failure("مرتبات الشهر ده اتعتمدت، مينفعش تتغير");

        _adjustments.Remove(adjustment);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    private Task<bool> MonthApprovedAsync(int year, int month)
        => _unitOfWork.Repository<PayrollRun>().QueryNoTracking()
            .AnyAsync(r => r.Year == year && r.Month == month && r.Status == PayrollRunStatus.Approved);
}
