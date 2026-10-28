using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.DAL.Repositories;
using HRSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.BLL.Services;

public interface IShiftService
{
    Task<List<ShiftDto>> GetAllAsync();
    Task<SaveShiftDto?> GetForEditAsync(int id);
    Task<ServiceResult> CreateAsync(SaveShiftDto dto);
    Task<ServiceResult> UpdateAsync(SaveShiftDto dto);
    Task<ServiceResult> DeleteAsync(int id);
}

public class ShiftService : IShiftService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericRepository<Shift> _shifts;

    public ShiftService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
        _shifts = unitOfWork.Repository<Shift>();
    }

    public Task<List<ShiftDto>> GetAllAsync()
        => _shifts.QueryNoTracking()
            .OrderBy(s => s.StartTime)
            .Select(s => new ShiftDto
            {
                Id = s.Id,
                Name = s.Name,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                GraceMinutes = s.GraceMinutes,
                EmployeesCount = s.Employees.Count
            })
            .ToListAsync();

    public Task<SaveShiftDto?> GetForEditAsync(int id)
        => _shifts.QueryNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new SaveShiftDto
            {
                Id = s.Id,
                Name = s.Name,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                GraceMinutes = s.GraceMinutes
            })
            .FirstOrDefaultAsync();

    public async Task<ServiceResult> CreateAsync(SaveShiftDto dto)
    {
        var validation = await ValidateAsync(dto);
        if (!validation.Succeeded)
            return validation;

        await _shifts.AddAsync(new Shift
        {
            Name = dto.Name.Trim(),
            StartTime = dto.StartTime!.Value,
            EndTime = dto.EndTime!.Value,
            GraceMinutes = dto.GraceMinutes
        });
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> UpdateAsync(SaveShiftDto dto)
    {
        var shift = await _shifts.GetByIdAsync(dto.Id);
        if (shift is null)
            return ServiceResult.Failure("الوردية مش موجودة");

        var validation = await ValidateAsync(dto);
        if (!validation.Succeeded)
            return validation;

        // Past records keep their minutes; the new times apply from the next check-in
        shift.Name = dto.Name.Trim();
        shift.StartTime = dto.StartTime!.Value;
        shift.EndTime = dto.EndTime!.Value;
        shift.GraceMinutes = dto.GraceMinutes;
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var shift = await _shifts.GetByIdAsync(id);
        if (shift is null)
            return ServiceResult.Failure("الوردية مش موجودة");

        if (await _unitOfWork.Repository<Employee>().QueryNoTracking().AnyAsync(e => e.ShiftId == id))
            return ServiceResult.Failure("مينفعش تمسح وردية عليها موظفين. انقلهم لوردية تانية الأول");

        _shifts.Remove(shift);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    private async Task<ServiceResult> ValidateAsync(SaveShiftDto dto)
    {
        // Night shifts crossing midnight aren't supported yet
        if (dto.EndTime <= dto.StartTime)
            return ServiceResult.Failure("نهاية الوردية لازم تكون بعد بدايتها في نفس اليوم", nameof(dto.EndTime));

        var name = dto.Name.Trim();
        if (await _shifts.QueryNoTracking().AnyAsync(s => s.Name == name && s.Id != dto.Id))
            return ServiceResult.Failure("في وردية تانية بنفس الاسم", nameof(dto.Name));

        return ServiceResult.Success();
    }
}
