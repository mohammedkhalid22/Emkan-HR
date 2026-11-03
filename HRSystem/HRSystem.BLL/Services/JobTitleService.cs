using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.DAL.Repositories;
using HRSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.BLL.Services;

public interface IJobTitleService
{
    Task<List<JobTitleDto>> GetAllAsync();
    Task<SaveJobTitleDto?> GetForEditAsync(int id);
    Task<List<LookupDto>> GetLookupAsync();
    Task<ServiceResult> CreateAsync(SaveJobTitleDto dto);
    Task<ServiceResult> UpdateAsync(SaveJobTitleDto dto);
    Task<ServiceResult> DeleteAsync(int id);
}

public class JobTitleService : IJobTitleService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericRepository<JobTitle> _jobTitles;

    public JobTitleService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
        _jobTitles = unitOfWork.Repository<JobTitle>();
    }

    public Task<List<JobTitleDto>> GetAllAsync()
        => _jobTitles.QueryNoTracking()
            .OrderBy(j => j.Name)
            .Select(j => new JobTitleDto
            {
                Id = j.Id,
                Name = j.Name,
                MinSalary = j.MinSalary,
                MaxSalary = j.MaxSalary,
                EmployeesCount = j.Employees.Count,
                HasRequirements = j.Requirements != null && j.Requirements != ""
            })
            .ToListAsync();

    public Task<SaveJobTitleDto?> GetForEditAsync(int id)
        => _jobTitles.QueryNoTracking()
            .Where(j => j.Id == id)
            .Select(j => new SaveJobTitleDto
            {
                Id = j.Id,
                Name = j.Name,
                MinSalary = j.MinSalary,
                MaxSalary = j.MaxSalary,
                Requirements = j.Requirements
            })
            .FirstOrDefaultAsync();

    public Task<List<LookupDto>> GetLookupAsync()
        => _jobTitles.QueryNoTracking()
            .OrderBy(j => j.Name)
            .Select(j => new LookupDto(j.Id, j.Name))
            .ToListAsync();

    public async Task<ServiceResult> CreateAsync(SaveJobTitleDto dto)
    {
        var validation = await ValidateAsync(dto);
        if (!validation.Succeeded)
            return validation;

        await _jobTitles.AddAsync(new JobTitle
        {
            Name = dto.Name.Trim(),
            MinSalary = dto.MinSalary,
            MaxSalary = dto.MaxSalary,
            Requirements = Clean(dto.Requirements)
        });
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> UpdateAsync(SaveJobTitleDto dto)
    {
        var jobTitle = await _jobTitles.GetByIdAsync(dto.Id);
        if (jobTitle is null)
            return ServiceResult.Failure("المسمى الوظيفي مش موجود");

        var validation = await ValidateAsync(dto);
        if (!validation.Succeeded)
            return validation;

        jobTitle.Name = dto.Name.Trim();
        jobTitle.MinSalary = dto.MinSalary;
        jobTitle.MaxSalary = dto.MaxSalary;
        jobTitle.Requirements = Clean(dto.Requirements);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var jobTitle = await _jobTitles.GetByIdAsync(id);
        if (jobTitle is null)
            return ServiceResult.Failure("المسمى الوظيفي مش موجود");

        var inUse = await _unitOfWork.Repository<Employee>().QueryNoTracking().AnyAsync(e => e.JobTitleId == id);
        if (inUse)
            return ServiceResult.Failure("مينفعش تمسح مسمى عليه موظفين");

        _jobTitles.Remove(jobTitle);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private async Task<ServiceResult> ValidateAsync(SaveJobTitleDto dto)
    {
        if (dto.MinSalary is decimal min && dto.MaxSalary is decimal max && min > max)
            return ServiceResult.Failure("الحد الأدنى لازم يكون أقل من أو يساوي الحد الأقصى", nameof(dto.MaxSalary));

        var name = dto.Name.Trim();
        if (await _jobTitles.QueryNoTracking().AnyAsync(j => j.Name == name && j.Id != dto.Id))
            return ServiceResult.Failure("في مسمى تاني بنفس الاسم", nameof(dto.Name));

        return ServiceResult.Success();
    }
}
