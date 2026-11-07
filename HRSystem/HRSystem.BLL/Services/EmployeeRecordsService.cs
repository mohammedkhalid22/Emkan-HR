using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.DAL.Repositories;
using HRSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.BLL.Services;

// The lists on an employee's profile: family, qualifications, past experience (HR edits them)
public interface IEmployeeRecordsService
{
    Task<ServiceResult> AddFamilyMemberAsync(FamilyMemberDto dto);
    Task<ServiceResult> AddQualificationAsync(QualificationDto dto);
    Task<ServiceResult> AddExperienceAsync(ExperienceDto dto);

    // Returns the employee the row belonged to, to go back to their profile
    Task<ServiceResult<int>> DeleteFamilyMemberAsync(int id);
    Task<ServiceResult<int>> DeleteQualificationAsync(int id);
    Task<ServiceResult<int>> DeleteExperienceAsync(int id);
}

public class EmployeeRecordsService : IEmployeeRecordsService
{
    private readonly IUnitOfWork _unitOfWork;

    public EmployeeRecordsService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult> AddFamilyMemberAsync(FamilyMemberDto dto)
    {
        if (!await EmployeeExistsAsync(dto.EmployeeId))
            return ServiceResult.Failure("الموظف مش موجود");
        if (dto.BirthDate is DateOnly birth && birth > Today)
            return ServiceResult.Failure("تاريخ الميلاد لسه مجاش");
        if (dto.NationalId is string id && !NationalId.TryParse(id, out _, out _))
            return ServiceResult.Failure("الرقم القومي مش صحيح");

        await _unitOfWork.Repository<EmployeeFamilyMember>().AddAsync(new EmployeeFamilyMember
        {
            EmployeeId = dto.EmployeeId,
            Name = dto.Name.Trim(),
            Relation = dto.Relation!.Value,
            BirthDate = dto.BirthDate,
            NationalId = string.IsNullOrWhiteSpace(dto.NationalId) ? null : dto.NationalId.Trim()
        });
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> AddQualificationAsync(QualificationDto dto)
    {
        if (!await EmployeeExistsAsync(dto.EmployeeId))
            return ServiceResult.Failure("الموظف مش موجود");
        if (dto.Year > Today.Year)
            return ServiceResult.Failure("السنة لسه مجتش");

        await _unitOfWork.Repository<EmployeeQualification>().AddAsync(new EmployeeQualification
        {
            EmployeeId = dto.EmployeeId,
            Type = dto.Type!.Value,
            Title = dto.Title.Trim(),
            Institution = dto.Institution?.Trim(),
            Year = dto.Year,
            Grade = dto.Grade?.Trim()
        });
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> AddExperienceAsync(ExperienceDto dto)
    {
        if (!await EmployeeExistsAsync(dto.EmployeeId))
            return ServiceResult.Failure("الموظف مش موجود");
        if (dto.FromDate is DateOnly from && dto.ToDate is DateOnly to && to < from)
            return ServiceResult.Failure("تاريخ النهاية لازم يكون بعد البداية");
        if (dto.ToDate > Today || dto.FromDate > Today)
            return ServiceResult.Failure("التاريخ لسه مجاش");

        await _unitOfWork.Repository<EmployeeExperience>().AddAsync(new EmployeeExperience
        {
            EmployeeId = dto.EmployeeId,
            Company = dto.Company.Trim(),
            JobTitle = dto.JobTitle?.Trim(),
            FromDate = dto.FromDate,
            ToDate = dto.ToDate,
            LeavingReason = dto.LeavingReason?.Trim()
        });
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public Task<ServiceResult<int>> DeleteFamilyMemberAsync(int id) => DeleteAsync<EmployeeFamilyMember>(id, f => f.EmployeeId);
    public Task<ServiceResult<int>> DeleteQualificationAsync(int id) => DeleteAsync<EmployeeQualification>(id, q => q.EmployeeId);
    public Task<ServiceResult<int>> DeleteExperienceAsync(int id) => DeleteAsync<EmployeeExperience>(id, x => x.EmployeeId);

    private async Task<ServiceResult<int>> DeleteAsync<T>(int id, Func<T, int> employeeOf) where T : Domain.Common.BaseEntity
    {
        var repository = _unitOfWork.Repository<T>();
        var row = await repository.GetByIdAsync(id);
        if (row is null)
            return ServiceResult<int>.Failure("السطر مش موجود");

        var employeeId = employeeOf(row);
        repository.Remove(row);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult<int>.Success(employeeId);
    }

    private Task<bool> EmployeeExistsAsync(int id)
        => _unitOfWork.Repository<Employee>().QueryNoTracking().AnyAsync(e => e.Id == id);

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
}
