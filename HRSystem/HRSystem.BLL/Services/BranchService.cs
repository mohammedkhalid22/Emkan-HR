using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.DAL.Repositories;
using HRSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.BLL.Services;

public interface IBranchService
{
    Task<List<BranchDto>> GetAllAsync();
    Task<SaveBranchDto?> GetForEditAsync(int id);
    Task<List<LookupDto>> GetLookupAsync();
    Task<ServiceResult> CreateAsync(SaveBranchDto dto);
    Task<ServiceResult> UpdateAsync(SaveBranchDto dto);
    Task<ServiceResult> DeleteAsync(int id);
}

public class BranchService : IBranchService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericRepository<Branch> _branches;

    public BranchService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
        _branches = unitOfWork.Repository<Branch>();
    }

    public Task<List<BranchDto>> GetAllAsync()
        => _branches.QueryNoTracking()
            .OrderBy(b => b.Name)
            .Select(b => new BranchDto
            {
                Id = b.Id,
                Name = b.Name,
                Address = b.Address,
                DepartmentsCount = b.Departments.Count
            })
            .ToListAsync();

    public Task<SaveBranchDto?> GetForEditAsync(int id)
        => _branches.QueryNoTracking()
            .Where(b => b.Id == id)
            .Select(b => new SaveBranchDto { Id = b.Id, Name = b.Name, Address = b.Address })
            .FirstOrDefaultAsync();

    public Task<List<LookupDto>> GetLookupAsync()
        => _branches.QueryNoTracking()
            .OrderBy(b => b.Name)
            .Select(b => new LookupDto(b.Id, b.Name))
            .ToListAsync();

    public async Task<ServiceResult> CreateAsync(SaveBranchDto dto)
    {
        var name = dto.Name.Trim();
        if (await NameExistsAsync(name, excludeId: 0))
            return ServiceResult.Failure("في فرع تاني بنفس الاسم", nameof(dto.Name));

        await _branches.AddAsync(new Branch { Name = name, Address = dto.Address?.Trim() });
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> UpdateAsync(SaveBranchDto dto)
    {
        var branch = await _branches.GetByIdAsync(dto.Id);
        if (branch is null)
            return ServiceResult.Failure("الفرع مش موجود");

        var name = dto.Name.Trim();
        if (await NameExistsAsync(name, excludeId: dto.Id))
            return ServiceResult.Failure("في فرع تاني بنفس الاسم", nameof(dto.Name));

        branch.Name = name;
        branch.Address = dto.Address?.Trim();
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var branch = await _branches.GetByIdAsync(id);
        if (branch is null)
            return ServiceResult.Failure("الفرع مش موجود");

        var hasDepartments = await _unitOfWork.Repository<Department>()
            .QueryNoTracking().AnyAsync(d => d.BranchId == id);
        if (hasDepartments)
            return ServiceResult.Failure("مينفعش تمسح فرع فيه أقسام. انقل الأقسام لفرع تاني الأول");

        _branches.Remove(branch);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    private Task<bool> NameExistsAsync(string name, int excludeId)
        => _branches.QueryNoTracking().AnyAsync(b => b.Name == name && b.Id != excludeId);
}
