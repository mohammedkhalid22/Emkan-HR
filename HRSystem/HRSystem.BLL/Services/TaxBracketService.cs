using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.DAL.Repositories;
using HRSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.BLL.Services;

public interface ITaxBracketService
{
    Task<List<TaxBracketDto>> GetByYearAsync(int year);
    Task<ServiceResult> AddAsync(SaveTaxBracketDto dto);
    Task<ServiceResult> DeleteAsync(int id);
    Task<ServiceResult> CopyYearAsync(int fromYear, int toYear);
}

public class TaxBracketService : ITaxBracketService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericRepository<TaxBracket> _brackets;

    public TaxBracketService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
        _brackets = unitOfWork.Repository<TaxBracket>();
    }

    public Task<List<TaxBracketDto>> GetByYearAsync(int year)
        => _brackets.QueryNoTracking()
            .Where(b => b.Year == year)
            .OrderBy(b => b.FromAmount)
            .Select(b => new TaxBracketDto { Id = b.Id, Year = b.Year, FromAmount = b.FromAmount, ToAmount = b.ToAmount, Rate = b.Rate })
            .ToListAsync();

    public async Task<ServiceResult> AddAsync(SaveTaxBracketDto dto)
    {
        var from = dto.FromAmount!.Value;
        var to = dto.ToAmount;
        if (to is decimal end && end <= from)
            return ServiceResult.Failure("\"إلى\" لازم يكون أكبر من \"من\"");

        // Bands must not overlap
        var existing = await GetByYearAsync(dto.Year);
        if (existing.Any(b => from < (b.ToAmount ?? decimal.MaxValue) && (to ?? decimal.MaxValue) > b.FromAmount))
            return ServiceResult.Failure("الشريحة دي متداخلة مع شريحة موجودة");

        await _brackets.AddAsync(new TaxBracket { Year = dto.Year, FromAmount = from, ToAmount = to, Rate = dto.Rate!.Value });
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var bracket = await _brackets.GetByIdAsync(id);
        if (bracket is null)
            return ServiceResult.Failure("الشريحة مش موجودة");

        _brackets.Remove(bracket);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> CopyYearAsync(int fromYear, int toYear)
    {
        if (await _brackets.QueryNoTracking().AnyAsync(b => b.Year == toYear))
            return ServiceResult.Failure($"سنة {toYear} فيها شرائح بالفعل");

        var source = await GetByYearAsync(fromYear);
        if (source.Count == 0)
            return ServiceResult.Failure($"مفيش شرائح في سنة {fromYear}");

        foreach (var b in source)
            await _brackets.AddAsync(new TaxBracket { Year = toYear, FromAmount = b.FromAmount, ToAmount = b.ToAmount, Rate = b.Rate });

        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }
}
