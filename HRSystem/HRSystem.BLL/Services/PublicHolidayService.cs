using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.DAL.Repositories;
using HRSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.BLL.Services;

public interface IPublicHolidayService
{
    Task<List<PublicHolidayDto>> GetByYearAsync(int year);
    Task<SavePublicHolidayDto?> GetForEditAsync(int id);
    Task<ServiceResult> CreateAsync(SavePublicHolidayDto dto);
    Task<ServiceResult> UpdateAsync(SavePublicHolidayDto dto);
    Task<ServiceResult> DeleteAsync(int id);
}

public class PublicHolidayService : IPublicHolidayService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericRepository<PublicHoliday> _holidays;

    public PublicHolidayService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
        _holidays = unitOfWork.Repository<PublicHoliday>();
    }

    public Task<List<PublicHolidayDto>> GetByYearAsync(int year)
        => _holidays.QueryNoTracking()
            .Where(h => h.Date.Year == year)
            .OrderBy(h => h.Date)
            .Select(h => new PublicHolidayDto { Id = h.Id, Date = h.Date, Name = h.Name })
            .ToListAsync();

    public Task<SavePublicHolidayDto?> GetForEditAsync(int id)
        => _holidays.QueryNoTracking()
            .Where(h => h.Id == id)
            .Select(h => new SavePublicHolidayDto { Id = h.Id, Date = h.Date, Name = h.Name })
            .FirstOrDefaultAsync();

    public async Task<ServiceResult> CreateAsync(SavePublicHolidayDto dto)
    {
        if (await DateTakenAsync(dto.Date!.Value, dto.Id))
            return ServiceResult.Failure("في عطلة متسجلة في نفس اليوم", nameof(dto.Date));

        await _holidays.AddAsync(new PublicHoliday { Date = dto.Date.Value, Name = dto.Name.Trim() });
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> UpdateAsync(SavePublicHolidayDto dto)
    {
        var holiday = await _holidays.GetByIdAsync(dto.Id);
        if (holiday is null)
            return ServiceResult.Failure("العطلة مش موجودة");
        if (await DateTakenAsync(dto.Date!.Value, dto.Id))
            return ServiceResult.Failure("في عطلة متسجلة في نفس اليوم", nameof(dto.Date));

        holiday.Date = dto.Date.Value;
        holiday.Name = dto.Name.Trim();
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var holiday = await _holidays.GetByIdAsync(id);
        if (holiday is null)
            return ServiceResult.Failure("العطلة مش موجودة");

        _holidays.Remove(holiday);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    private Task<bool> DateTakenAsync(DateOnly date, int excludeId)
        => _holidays.QueryNoTracking().AnyAsync(h => h.Date == date && h.Id != excludeId);
}
