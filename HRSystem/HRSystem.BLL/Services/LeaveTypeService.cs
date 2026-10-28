using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.DAL.Repositories;
using HRSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.BLL.Services;

public interface ILeaveTypeService
{
    Task<List<LeaveTypeDto>> GetAllAsync();
    Task<SaveLeaveTypeDto?> GetForEditAsync(int id);
    Task<List<LookupDto>> GetLookupAsync();
    Task<ServiceResult> CreateAsync(SaveLeaveTypeDto dto);
    Task<ServiceResult> UpdateAsync(SaveLeaveTypeDto dto);
    Task<ServiceResult> DeleteAsync(int id);
}

public class LeaveTypeService : ILeaveTypeService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericRepository<LeaveType> _types;

    public LeaveTypeService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
        _types = unitOfWork.Repository<LeaveType>();
    }

    public async Task<List<LeaveTypeDto>> GetAllAsync()
    {
        var types = await _types.QueryNoTracking().OrderBy(t => t.Name).ToListAsync();
        var counts = await _unitOfWork.Repository<LeaveRequest>().QueryNoTracking()
            .GroupBy(r => r.LeaveTypeId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        return types.Select(t => new LeaveTypeDto
        {
            Id = t.Id,
            Name = t.Name,
            AnnualDays = t.AnnualDays,
            SeniorAnnualDays = t.SeniorAnnualDays,
            IsPaid = t.IsPaid,
            RequiresAttachment = t.RequiresAttachment,
            RequestsCount = counts.GetValueOrDefault(t.Id)
        }).ToList();
    }

    public Task<SaveLeaveTypeDto?> GetForEditAsync(int id)
        => _types.QueryNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new SaveLeaveTypeDto
            {
                Id = t.Id,
                Name = t.Name,
                AnnualDays = t.AnnualDays,
                SeniorAnnualDays = t.SeniorAnnualDays,
                IsPaid = t.IsPaid,
                RequiresAttachment = t.RequiresAttachment
            })
            .FirstOrDefaultAsync();

    public Task<List<LookupDto>> GetLookupAsync()
        => _types.QueryNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new LookupDto(t.Id, t.Name))
            .ToListAsync();

    public async Task<ServiceResult> CreateAsync(SaveLeaveTypeDto dto)
    {
        var validation = await ValidateAsync(dto);
        if (!validation.Succeeded)
            return validation;

        var type = new LeaveType();
        Map(dto, type);
        await _types.AddAsync(type);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> UpdateAsync(SaveLeaveTypeDto dto)
    {
        var type = await _types.GetByIdAsync(dto.Id);
        if (type is null)
            return ServiceResult.Failure("نوع الإجازة مش موجود");

        var validation = await ValidateAsync(dto);
        if (!validation.Succeeded)
            return validation;

        // Existing balances keep their numbers; the new days apply when next year's balances are generated
        Map(dto, type);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var type = await _types.GetByIdAsync(id);
        if (type is null)
            return ServiceResult.Failure("نوع الإجازة مش موجود");

        if (await _unitOfWork.Repository<LeaveRequest>().QueryNoTracking().AnyAsync(r => r.LeaveTypeId == id))
            return ServiceResult.Failure("مينفعش تمسح نوع عليه طلبات إجازة");

        _types.Remove(type);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    private static void Map(SaveLeaveTypeDto dto, LeaveType type)
    {
        type.Name = dto.Name.Trim();
        type.AnnualDays = dto.AnnualDays;
        type.SeniorAnnualDays = dto.AnnualDays == 0 ? null : dto.SeniorAnnualDays;
        type.IsPaid = dto.IsPaid;
        type.RequiresAttachment = dto.RequiresAttachment;
    }

    private async Task<ServiceResult> ValidateAsync(SaveLeaveTypeDto dto)
    {
        if (dto.SeniorAnnualDays is int senior && senior < dto.AnnualDays)
            return ServiceResult.Failure("رصيد الأقدمية لازم يكون أكبر من أو يساوي الرصيد العادي", nameof(dto.SeniorAnnualDays));

        var name = dto.Name.Trim();
        if (await _types.QueryNoTracking().AnyAsync(t => t.Name == name && t.Id != dto.Id))
            return ServiceResult.Failure("في نوع تاني بنفس الاسم", nameof(dto.Name));

        return ServiceResult.Success();
    }
}
