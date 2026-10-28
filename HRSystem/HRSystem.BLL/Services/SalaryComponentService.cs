using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.DAL.Repositories;
using HRSystem.Domain.Entities;
using HRSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.BLL.Services;

public interface ISalaryComponentService
{
    Task<List<SalaryComponentDto>> GetAllAsync();
    Task<SaveSalaryComponentDto?> GetForEditAsync(int id);
    Task<List<LookupDto>> GetLookupAsync();
    Task<ServiceResult> CreateAsync(SaveSalaryComponentDto dto);
    Task<ServiceResult> UpdateAsync(SaveSalaryComponentDto dto);
    Task<ServiceResult> DeleteAsync(int id);

    // Components given to one employee
    Task<List<EmployeeComponentDto>> GetForEmployeeAsync(int employeeId);
    Task<ServiceResult> SetForEmployeeAsync(int employeeId, int componentId, decimal value);
    Task<ServiceResult<int>> RemoveFromEmployeeAsync(int employeeComponentId);
}

public class SalaryComponentService : ISalaryComponentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericRepository<SalaryComponent> _components;
    private readonly IGenericRepository<EmployeeSalaryComponent> _employeeComponents;

    public SalaryComponentService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
        _components = unitOfWork.Repository<SalaryComponent>();
        _employeeComponents = unitOfWork.Repository<EmployeeSalaryComponent>();
    }

    public async Task<List<SalaryComponentDto>> GetAllAsync()
    {
        var counts = await _employeeComponents.QueryNoTracking()
            .GroupBy(c => c.SalaryComponentId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        return (await _components.QueryNoTracking().OrderBy(c => c.Type).ThenBy(c => c.Name).ToListAsync())
            .Select(c => new SalaryComponentDto
            {
                Id = c.Id,
                Name = c.Name,
                Type = c.Type,
                CalcMethod = c.CalcMethod,
                IsTaxable = c.IsTaxable,
                EmployeesCount = counts.GetValueOrDefault(c.Id)
            }).ToList();
    }

    public Task<SaveSalaryComponentDto?> GetForEditAsync(int id)
        => _components.QueryNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new SaveSalaryComponentDto { Id = c.Id, Name = c.Name, Type = c.Type, CalcMethod = c.CalcMethod, IsTaxable = c.IsTaxable })
            .FirstOrDefaultAsync();

    public Task<List<LookupDto>> GetLookupAsync()
        => _components.QueryNoTracking()
            .OrderBy(c => c.Type).ThenBy(c => c.Name)
            .Select(c => new LookupDto(c.Id, c.Name + (c.CalcMethod == CalculationMethod.Percentage ? " (%)" : "")))
            .ToListAsync();

    public async Task<ServiceResult> CreateAsync(SaveSalaryComponentDto dto)
    {
        if (await NameTakenAsync(dto))
            return ServiceResult.Failure("في مكوّن تاني بنفس الاسم", nameof(dto.Name));

        await _components.AddAsync(new SalaryComponent
        {
            Name = dto.Name.Trim(),
            Type = dto.Type!.Value,
            CalcMethod = dto.CalcMethod!.Value,
            IsTaxable = dto.IsTaxable,
            IsInsurable = false
        });
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> UpdateAsync(SaveSalaryComponentDto dto)
    {
        var component = await _components.GetByIdAsync(dto.Id);
        if (component is null)
            return ServiceResult.Failure("المكوّن مش موجود");
        if (await NameTakenAsync(dto))
            return ServiceResult.Failure("في مكوّن تاني بنفس الاسم", nameof(dto.Name));

        component.Name = dto.Name.Trim();
        component.Type = dto.Type!.Value;
        component.CalcMethod = dto.CalcMethod!.Value;
        component.IsTaxable = dto.IsTaxable;
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var component = await _components.GetByIdAsync(id);
        if (component is null)
            return ServiceResult.Failure("المكوّن مش موجود");
        if (await _employeeComponents.QueryNoTracking().AnyAsync(c => c.SalaryComponentId == id))
            return ServiceResult.Failure("مينفعش تمسح مكوّن متضاف لموظفين. شيله منهم الأول");

        _components.Remove(component);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public Task<List<EmployeeComponentDto>> GetForEmployeeAsync(int employeeId)
        => _employeeComponents.QueryNoTracking()
            .Where(c => c.EmployeeId == employeeId)
            .OrderBy(c => c.SalaryComponent.Type).ThenBy(c => c.SalaryComponent.Name)
            .Select(c => new EmployeeComponentDto
            {
                Id = c.Id,
                Name = c.SalaryComponent.Name,
                Type = c.SalaryComponent.Type,
                CalcMethod = c.SalaryComponent.CalcMethod,
                Value = c.Value
            })
            .ToListAsync();

    // Adds the component, or changes its value if the employee already has it
    public async Task<ServiceResult> SetForEmployeeAsync(int employeeId, int componentId, decimal value)
    {
        var component = await _components.QueryNoTracking().FirstOrDefaultAsync(c => c.Id == componentId);
        if (component is null)
            return ServiceResult.Failure("اختار المكوّن");
        if (value <= 0)
            return ServiceResult.Failure("القيمة لازم تكون أكبر من صفر");
        if (component.CalcMethod == CalculationMethod.Percentage && value > 100)
            return ServiceResult.Failure("النسبة لازم تكون 100% أو أقل");

        var existing = await _employeeComponents.Query()
            .FirstOrDefaultAsync(c => c.EmployeeId == employeeId && c.SalaryComponentId == componentId);
        if (existing is null)
            await _employeeComponents.AddAsync(new EmployeeSalaryComponent { EmployeeId = employeeId, SalaryComponentId = componentId, Value = value });
        else
            existing.Value = value;

        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult<int>> RemoveFromEmployeeAsync(int employeeComponentId)
    {
        var item = await _employeeComponents.GetByIdAsync(employeeComponentId);
        if (item is null)
            return ServiceResult<int>.Failure("المكوّن مش موجود");

        _employeeComponents.Remove(item);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult<int>.Success(item.EmployeeId);
    }

    private Task<bool> NameTakenAsync(SaveSalaryComponentDto dto)
    {
        var name = dto.Name.Trim();
        return _components.QueryNoTracking().AnyAsync(c => c.Name == name && c.Id != dto.Id);
    }
}
