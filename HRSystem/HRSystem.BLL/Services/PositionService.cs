using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.DAL.Repositories;
using HRSystem.Domain.Entities;
using HRSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.BLL.Services;

// Positions are the seats of the org chart (SAP style): administration → section → position,
// each position reporting to a higher one. Holders' direct managers follow from the tree.
public interface IPositionService
{
    Task<List<PositionDto>> GetAllAsync(int? departmentId = null);
    Task<SavePositionDto?> GetForEditAsync(int id);
    Task<List<PositionOptionDto>> GetOptionsAsync();

    // Positions that may be the parent of positionId (itself and those under it left out)
    Task<List<LookupDto>> GetParentLookupAsync(int? positionId);

    Task<ServiceResult> CreateAsync(SavePositionDto dto);
    Task<ServiceResult> UpdateAsync(SavePositionDto dto);
    Task<ServiceResult> DeleteAsync(int id);

    Task<List<OrgUnitNode>> GetTreeAsync();
}

public class PositionService : IPositionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericRepository<Position> _positions;

    public PositionService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
        _positions = unitOfWork.Repository<Position>();
    }

    public async Task<List<PositionDto>> GetAllAsync(int? departmentId = null)
    {
        var list = await _positions.QueryNoTracking()
            .Where(p => departmentId == null || p.DepartmentId == departmentId)
            .OrderBy(p => p.Department.Name).ThenBy(p => p.Name)
            .Select(p => new PositionDto
            {
                Id = p.Id,
                Name = p.Name,
                DepartmentId = p.DepartmentId,
                DepartmentName = p.Department.Name,
                JobTitleName = p.JobTitle.Name,
                ParentPositionId = p.ParentPositionId,
                ParentPositionName = p.ParentPosition != null ? p.ParentPosition.Name : null,
                Capacity = p.Capacity
            })
            .ToListAsync();

        var holders = await HoldersAsync();
        list.ForEach(p => p.Holders = holders.GetValueOrDefault(p.Id) ?? []);
        return list;
    }

    public Task<SavePositionDto?> GetForEditAsync(int id)
        => _positions.QueryNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new SavePositionDto
            {
                Id = p.Id,
                Name = p.Name,
                DepartmentId = p.DepartmentId,
                JobTitleId = p.JobTitleId,
                ParentPositionId = p.ParentPositionId,
                Capacity = p.Capacity
            })
            .FirstOrDefaultAsync();

    public async Task<List<PositionOptionDto>> GetOptionsAsync()
    {
        var holders = await HoldersAsync();
        var list = await _positions.QueryNoTracking()
            .OrderBy(p => p.Department.Name).ThenBy(p => p.Name)
            .Select(p => new { p.Id, p.Name, p.DepartmentId, Department = p.Department.Name, p.JobTitleId, p.Capacity })
            .ToListAsync();
        return list.Select(p => new PositionOptionDto(p.Id, p.Name, p.DepartmentId, p.Department, p.JobTitleId,
            holders.GetValueOrDefault(p.Id)?.Count ?? 0, p.Capacity)).ToList();
    }

    public async Task<List<LookupDto>> GetParentLookupAsync(int? positionId)
    {
        var all = await _positions.QueryNoTracking()
            .OrderBy(p => p.Department.Name).ThenBy(p => p.Name)
            .Select(p => new { p.Id, p.ParentPositionId, Label = p.Name + " — " + p.Department.Name })
            .ToListAsync();
        if (positionId is null)
            return all.Select(p => new LookupDto(p.Id, p.Label)).ToList();

        var parents = all.ToDictionary(p => p.Id, p => p.ParentPositionId);
        return all.Where(p => !IsUnder(p.Id, positionId.Value, parents))
            .Select(p => new LookupDto(p.Id, p.Label)).ToList();
    }

    public async Task<ServiceResult> CreateAsync(SavePositionDto dto)
    {
        var validation = await ValidateAsync(dto);
        if (!validation.Succeeded)
            return validation;

        await _positions.AddAsync(new Position
        {
            Name = dto.Name.Trim(),
            DepartmentId = dto.DepartmentId!.Value,
            JobTitleId = dto.JobTitleId!.Value,
            ParentPositionId = dto.ParentPositionId,
            Capacity = dto.Capacity
        });
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> UpdateAsync(SavePositionDto dto)
    {
        var position = await _positions.GetByIdAsync(dto.Id);
        if (position is null)
            return ServiceResult.Failure("الموقع الوظيفي مش موجود");

        var validation = await ValidateAsync(dto);
        if (!validation.Succeeded)
            return validation;

        var moved = position.DepartmentId != dto.DepartmentId || position.JobTitleId != dto.JobTitleId;
        position.Name = dto.Name.Trim();
        position.DepartmentId = dto.DepartmentId!.Value;
        position.JobTitleId = dto.JobTitleId!.Value;
        position.ParentPositionId = dto.ParentPositionId;
        position.Capacity = dto.Capacity;

        // Holders move with their position
        if (moved)
        {
            var holders = await _unitOfWork.Repository<Employee>().Query().Where(e => e.PositionId == position.Id).ToListAsync();
            foreach (var e in holders)
            {
                e.DepartmentId = position.DepartmentId;
                e.JobTitleId = position.JobTitleId;
            }
        }

        await _unitOfWork.SaveChangesAsync();
        await OrgManagers.SyncAsync(_unitOfWork);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var info = await _positions.QueryNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new { HasHolders = p.Employees.Any(), HasChildren = p.SubPositions.Any() })
            .FirstOrDefaultAsync();
        if (info is null)
            return ServiceResult.Failure("الموقع الوظيفي مش موجود");
        if (info.HasHolders)
            return ServiceResult.Failure("مينفعش تمسح موقع عليه موظفين. انقلهم لموقع تاني الأول");
        if (info.HasChildren)
            return ServiceResult.Failure("مينفعش تمسح موقع تابع ليه مواقع تانية. غيّر تبعيتها الأول");

        _positions.Remove((await _positions.GetByIdAsync(id))!);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    // ---------- The tree ----------

    public async Task<List<OrgUnitNode>> GetTreeAsync()
    {
        var units = await _unitOfWork.Repository<Department>().QueryNoTracking()
            .OrderBy(d => d.Type).ThenBy(d => d.Name)
            .Select(d => new
            {
                d.Id, d.Name, d.Type, d.ParentDepartmentId,
                Branch = d.Branch != null ? d.Branch.Name : null,
                Employees = d.Employees.Count(e => e.Status != EmployeeStatus.Terminated)
            })
            .ToListAsync();

        var positions = await _positions.QueryNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                p.Id, p.Name, p.DepartmentId, p.ParentPositionId, p.Capacity,
                JobTitle = p.JobTitle.Name,
                ParentDepartmentId = p.ParentPosition != null ? (int?)p.ParentPosition.DepartmentId : null,
                ParentName = p.ParentPosition != null ? p.ParentPosition.Name : null
            })
            .ToListAsync();

        var holders = await HoldersAsync();
        var nodes = positions.ToDictionary(p => p.Id, p => new OrgPositionNode
        {
            Id = p.Id,
            Name = p.Name,
            JobTitleName = p.JobTitle,
            Capacity = p.Capacity,
            Holders = holders.GetValueOrDefault(p.Id) ?? [],
            ReportsToElsewhere = p.ParentPositionId != null && p.ParentDepartmentId != p.DepartmentId ? p.ParentName : null
        });

        // A position hangs under its parent when both are in the same unit, otherwise it starts its unit's list
        foreach (var p in positions.Where(p => p.ParentPositionId is int parent && p.ParentDepartmentId == p.DepartmentId))
            nodes[p.ParentPositionId!.Value].Children.Add(nodes[p.Id]);

        var unitNodes = units.ToDictionary(u => u.Id, u => new OrgUnitNode
        {
            Id = u.Id,
            Name = u.Name,
            Type = u.Type,
            BranchName = u.Branch,
            EmployeesCount = u.Employees,
            Positions = positions.Where(p => p.DepartmentId == u.Id && (p.ParentPositionId is null || p.ParentDepartmentId != u.Id))
                .Select(p => nodes[p.Id]).ToList()
        });

        var roots = new List<OrgUnitNode>();
        foreach (var u in units)
        {
            if (u.ParentDepartmentId is int parent && unitNodes.TryGetValue(parent, out var parentNode))
                parentNode.Children.Add(unitNodes[u.Id]);
            else
                roots.Add(unitNodes[u.Id]);
        }
        return roots;
    }

    // ---------- Helpers ----------

    private async Task<ServiceResult> ValidateAsync(SavePositionDto dto)
    {
        var name = dto.Name.Trim();
        if (!await _unitOfWork.Repository<Department>().QueryNoTracking().AnyAsync(d => d.Id == dto.DepartmentId))
            return ServiceResult.Failure("الإدارة / القسم مش موجود", nameof(dto.DepartmentId));
        if (!await _unitOfWork.Repository<JobTitle>().QueryNoTracking().AnyAsync(j => j.Id == dto.JobTitleId))
            return ServiceResult.Failure("المسمى الوظيفي مش موجود", nameof(dto.JobTitleId));
        if (await _positions.QueryNoTracking().AnyAsync(p => p.DepartmentId == dto.DepartmentId && p.Name == name && p.Id != dto.Id))
            return ServiceResult.Failure("فيه موقع بنفس الاسم في نفس القسم", nameof(dto.Name));

        if (dto.ParentPositionId is int parentId)
        {
            var parents = await _positions.QueryNoTracking().ToDictionaryAsync(p => p.Id, p => p.ParentPositionId);
            if (!parents.ContainsKey(parentId))
                return ServiceResult.Failure("الوظيفة الأعلى مش موجودة", nameof(dto.ParentPositionId));
            if (dto.Id != 0 && IsUnder(parentId, dto.Id, parents))
                return ServiceResult.Failure("مينفعش الموقع يبقى تابع لنفسه أو لموقع تحته", nameof(dto.ParentPositionId));
        }

        if (dto.Id != 0)
        {
            var holders = await _unitOfWork.Repository<Employee>().QueryNoTracking()
                .CountAsync(e => e.PositionId == dto.Id && e.Status != EmployeeStatus.Terminated);
            if (holders > dto.Capacity)
                return ServiceResult.Failure($"على الموقع ده {holders} موظفين، فمينفعش العدد المسموح يبقى أقل من كده", nameof(dto.Capacity));
        }

        return ServiceResult.Success();
    }

    // True when candidate is positionId itself or somewhere under it
    private static bool IsUnder(int candidate, int positionId, Dictionary<int, int?> parents)
    {
        int? current = candidate;
        var visited = new HashSet<int>();
        while (current is int id && visited.Add(id))
        {
            if (id == positionId)
                return true;
            current = parents.GetValueOrDefault(id);
        }
        return false;
    }

    private async Task<Dictionary<int, List<LookupDto>>> HoldersAsync()
        => (await _unitOfWork.Repository<Employee>().QueryNoTracking()
                .Where(e => e.PositionId != null && e.Status != EmployeeStatus.Terminated)
                .OrderBy(e => e.HireDate).ThenBy(e => e.Id)
                .Select(e => new { PositionId = e.PositionId!.Value, e.Id, e.FullNameAr })
                .ToListAsync())
            .GroupBy(e => e.PositionId)
            .ToDictionary(g => g.Key, g => g.Select(e => new LookupDto(e.Id, e.FullNameAr)).ToList());
}

// Keeps Employee.ManagerId in step with the position tree: a holder's direct manager is the first
// active holder found walking up from their position (the earliest hired one when a seat is shared).
// Employees without a position keep the manager picked by hand.
public static class OrgManagers
{
    public static async Task<int> SyncAsync(IUnitOfWork unitOfWork)
    {
        var parents = await unitOfWork.Repository<Position>().QueryNoTracking()
            .ToDictionaryAsync(p => p.Id, p => p.ParentPositionId);
        var employees = unitOfWork.Repository<Employee>();
        var holders = await employees.Query()
            .Where(e => e.PositionId != null && e.Status != EmployeeStatus.Terminated)
            .OrderBy(e => e.HireDate).ThenBy(e => e.Id)
            .ToListAsync();
        var first = holders.GroupBy(e => e.PositionId!.Value).ToDictionary(g => g.Key, g => g.First());
        var names = await employees.QueryNoTracking().ToDictionaryAsync(e => e.Id, e => e.FullNameAr);

        var changed = 0;
        foreach (var employee in holders)
        {
            int? manager = null;
            int? current = parents.GetValueOrDefault(employee.PositionId!.Value);
            var visited = new HashSet<int>();
            while (current is int id && visited.Add(id))
            {
                if (first.TryGetValue(id, out var holder) && holder.Id != employee.Id)
                {
                    manager = holder.Id;
                    break;
                }
                current = parents.GetValueOrDefault(id);
            }

            if (employee.ManagerId == manager)
                continue;

            employee.History.Add(new EmployeeHistory
            {
                ChangeType = "تغيير المدير",
                OldValue = employee.ManagerId is int old ? names.GetValueOrDefault(old) : "بدون",
                NewValue = manager is int m ? names.GetValueOrDefault(m) : "بدون",
                EffectiveDate = DateOnly.FromDateTime(DateTime.Today)
            });
            employee.ManagerId = manager;
            changed++;
        }

        if (changed > 0)
            await unitOfWork.SaveChangesAsync();
        return changed;
    }
}
