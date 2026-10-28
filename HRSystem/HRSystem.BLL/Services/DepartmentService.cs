using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.DAL.Repositories;
using HRSystem.Domain.Entities;
using HRSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.BLL.Services;

public interface IDepartmentService
{
    Task<List<DepartmentDto>> GetAllAsync();
    Task<SaveDepartmentDto?> GetForEditAsync(int id);
    Task<List<LookupDto>> GetLookupAsync(int? excludeId = null);
    Task<List<LookupDto>> GetManagerCandidatesAsync();
    Task<ServiceResult> CreateAsync(SaveDepartmentDto dto);
    Task<ServiceResult> UpdateAsync(SaveDepartmentDto dto);
    Task<ServiceResult> DeleteAsync(int id);
}

public class DepartmentService : IDepartmentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericRepository<Department> _departments;

    public DepartmentService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
        _departments = unitOfWork.Repository<Department>();
    }

    public Task<List<DepartmentDto>> GetAllAsync()
        => _departments.QueryNoTracking()
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentDto
            {
                Id = d.Id,
                Name = d.Name,
                ParentDepartmentName = d.ParentDepartment != null ? d.ParentDepartment.Name : null,
                BranchName = d.Branch != null ? d.Branch.Name : null,
                ManagerName = d.Manager != null ? d.Manager.FullNameAr : null,
                EmployeesCount = d.Employees.Count,
                SubDepartmentsCount = d.SubDepartments.Count
            })
            .ToListAsync();

    public Task<SaveDepartmentDto?> GetForEditAsync(int id)
        => _departments.QueryNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new SaveDepartmentDto
            {
                Id = d.Id,
                Name = d.Name,
                ParentDepartmentId = d.ParentDepartmentId,
                BranchId = d.BranchId,
                ManagerId = d.ManagerId
            })
            .FirstOrDefaultAsync();

    public Task<List<LookupDto>> GetLookupAsync(int? excludeId = null)
        => _departments.QueryNoTracking()
            .Where(d => excludeId == null || d.Id != excludeId)
            .OrderBy(d => d.Name)
            .Select(d => new LookupDto(d.Id, d.Name))
            .ToListAsync();

    public Task<List<LookupDto>> GetManagerCandidatesAsync()
        => _unitOfWork.Repository<Employee>().QueryNoTracking()
            .Where(e => e.Status == EmployeeStatus.Active)
            .OrderBy(e => e.FullNameAr)
            .Select(e => new LookupDto(e.Id, e.Code + " - " + e.FullNameAr))
            .ToListAsync();

    public async Task<ServiceResult> CreateAsync(SaveDepartmentDto dto)
    {
        var validation = await ValidateAsync(dto);
        if (!validation.Succeeded)
            return validation;

        await _departments.AddAsync(new Department
        {
            Name = dto.Name.Trim(),
            ParentDepartmentId = dto.ParentDepartmentId,
            BranchId = dto.BranchId,
            ManagerId = dto.ManagerId
        });
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> UpdateAsync(SaveDepartmentDto dto)
    {
        var department = await _departments.GetByIdAsync(dto.Id);
        if (department is null)
            return ServiceResult.Failure("القسم مش موجود");

        var validation = await ValidateAsync(dto);
        if (!validation.Succeeded)
            return validation;

        department.Name = dto.Name.Trim();
        department.ParentDepartmentId = dto.ParentDepartmentId;
        department.BranchId = dto.BranchId;
        department.ManagerId = dto.ManagerId;
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var info = await _departments.QueryNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new { HasEmployees = d.Employees.Any(), HasChildren = d.SubDepartments.Any() })
            .FirstOrDefaultAsync();

        if (info is null)
            return ServiceResult.Failure("القسم مش موجود");
        if (info.HasEmployees)
            return ServiceResult.Failure("مينفعش تمسح قسم فيه موظفين. انقلهم لقسم تاني الأول");
        if (info.HasChildren)
            return ServiceResult.Failure("مينفعش تمسح قسم تحته أقسام فرعية");

        var department = await _departments.GetByIdAsync(id);
        _departments.Remove(department!);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    private async Task<ServiceResult> ValidateAsync(SaveDepartmentDto dto)
    {
        var name = dto.Name.Trim();
        if (await _departments.QueryNoTracking().AnyAsync(d => d.Name == name && d.Id != dto.Id))
            return ServiceResult.Failure("في قسم تاني بنفس الاسم", nameof(dto.Name));

        if (dto.ParentDepartmentId is int parentId)
        {
            if (dto.Id != 0 && await IsSelfOrDescendantAsync(parentId, dto.Id))
                return ServiceResult.Failure("مينفعش القسم يبقى تابع لنفسه أو لقسم من الأقسام اللي تحته",
                    nameof(dto.ParentDepartmentId));
        }

        if (dto.ManagerId is int managerId)
        {
            var managerIsActive = await _unitOfWork.Repository<Employee>().QueryNoTracking()
                .AnyAsync(e => e.Id == managerId && e.Status == EmployeeStatus.Active);
            if (!managerIsActive)
                return ServiceResult.Failure("المدير لازم يكون موظف نشط", nameof(dto.ManagerId));
        }

        return ServiceResult.Success();
    }

    // Walks up from candidateParentId; if we reach departmentId, the new parent is inside its own subtree
    private async Task<bool> IsSelfOrDescendantAsync(int candidateParentId, int departmentId)
    {
        var parents = await _departments.QueryNoTracking()
            .ToDictionaryAsync(d => d.Id, d => d.ParentDepartmentId);

        int? current = candidateParentId;
        var visited = new HashSet<int>();
        while (current is int id && visited.Add(id))
        {
            if (id == departmentId)
                return true;
            current = parents.GetValueOrDefault(id);
        }

        return false;
    }
}
