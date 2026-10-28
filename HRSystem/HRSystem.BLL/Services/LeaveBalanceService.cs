using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.DAL.Repositories;
using HRSystem.Domain.Entities;
using HRSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.BLL.Services;

public interface ILeaveBalanceService
{
    Task<List<LeaveBalanceRowDto>> GetAsync(LeaveBalanceFilterDto filter);
    Task<List<MyBalanceDto>> GetForEmployeeAsync(int employeeId, int year);
    Task<int> GenerateAsync(int year);
    Task<ServiceResult> UpdateEntitledAsync(int balanceId, decimal entitled);

    // Used by requests: the balance row for a limited type, created on first use
    Task<LeaveBalance?> EnsureAsync(int employeeId, LeaveType type, int year);
    Task<decimal> PendingDaysAsync(int employeeId, int leaveTypeId, int year, int? excludeRequestId = null);
}

public class LeaveBalanceService : ILeaveBalanceService
{
    // Requests that hold days until a decision is made
    private static readonly RequestStatus[] Waiting = [RequestStatus.Pending, RequestStatus.ManagerApproved];

    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericRepository<LeaveBalance> _balances;

    public LeaveBalanceService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
        _balances = unitOfWork.Repository<LeaveBalance>();
    }

    public async Task<List<LeaveBalanceRowDto>> GetAsync(LeaveBalanceFilterDto filter)
    {
        var query = _balances.QueryNoTracking().Where(b => b.Year == filter.Year);

        if (filter.DepartmentId is int departmentId)
            query = query.Where(b => b.Employee.DepartmentId == departmentId);
        if (filter.LeaveTypeId is int typeId)
            query = query.Where(b => b.LeaveTypeId == typeId);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(b => b.Employee.FullNameAr.Contains(term) || b.Employee.Code.Contains(term));
        }

        var rows = await query
            .OrderBy(b => b.Employee.Code).ThenBy(b => b.LeaveType.Name)
            .Select(b => new LeaveBalanceRowDto
            {
                Id = b.Id,
                EmployeeId = b.EmployeeId,
                EmployeeCode = b.Employee.Code,
                EmployeeName = b.Employee.FullNameAr,
                DepartmentName = b.Employee.Department.Name,
                LeaveTypeName = b.LeaveType.Name,
                Entitled = b.Entitled,
                Used = b.Used
            })
            .ToListAsync();

        // Days held by requests still waiting for a decision, per employee + type
        var pending = await _unitOfWork.Repository<LeaveRequest>().QueryNoTracking()
            .Where(r => Waiting.Contains(r.Status) && r.StartDate.Year == filter.Year)
            .GroupBy(r => new { r.EmployeeId, r.LeaveType.Name })
            .Select(g => new { g.Key.EmployeeId, g.Key.Name, Days = g.Sum(r => r.Days) })
            .ToListAsync();

        foreach (var row in rows)
            row.Pending = pending.FirstOrDefault(p => p.EmployeeId == row.EmployeeId && p.Name == row.LeaveTypeName)?.Days ?? 0;

        return rows;
    }

    public async Task<List<MyBalanceDto>> GetForEmployeeAsync(int employeeId, int year)
    {
        var types = await _unitOfWork.Repository<LeaveType>().Query().OrderBy(t => t.Name).ToListAsync();
        var result = new List<MyBalanceDto>();

        foreach (var type in types)
        {
            var balance = await EnsureAsync(employeeId, type, year);
            var used = balance?.Used ?? await UsedDaysAsync(employeeId, type.Id, year);

            result.Add(new MyBalanceDto
            {
                LeaveTypeId = type.Id,
                LeaveTypeName = type.Name,
                IsLimited = balance is not null,
                Entitled = balance?.Entitled ?? 0,
                Used = used,
                Pending = await PendingDaysAsync(employeeId, type.Id, year)
            });
        }

        // EnsureAsync may have created rows
        await _unitOfWork.SaveChangesAsync();
        return result;
    }

    public async Task<int> GenerateAsync(int year)
    {
        var yearEnd = new DateOnly(year, 12, 31);
        var employeeIds = await _unitOfWork.Repository<Employee>().QueryNoTracking()
            .Where(e => e.Status == EmployeeStatus.Active && e.HireDate <= yearEnd)
            .Select(e => e.Id)
            .ToListAsync();
        var types = await _unitOfWork.Repository<LeaveType>().QueryNoTracking()
            .Where(t => t.AnnualDays > 0)
            .ToListAsync();

        var created = 0;
        foreach (var employeeId in employeeIds)
        foreach (var type in types)
        {
            var exists = await _balances.QueryNoTracking()
                .AnyAsync(b => b.EmployeeId == employeeId && b.LeaveTypeId == type.Id && b.Year == year);
            if (!exists && await EnsureAsync(employeeId, type, year) is not null)
                created++;
        }

        await _unitOfWork.SaveChangesAsync();
        return created;
    }

    public async Task<ServiceResult> UpdateEntitledAsync(int balanceId, decimal entitled)
    {
        var balance = await _balances.GetByIdAsync(balanceId);
        if (balance is null)
            return ServiceResult.Failure("الرصيد مش موجود");
        if (entitled < 0 || entitled > 365)
            return ServiceResult.Failure("الرصيد لازم يكون بين 0 و 365");
        if (entitled < balance.Used)
            return ServiceResult.Failure($"الرصيد مينفعش يقل عن الأيام المستخدمة ({balance.Used:0.#})");

        balance.Entitled = entitled;
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<LeaveBalance?> EnsureAsync(int employeeId, LeaveType type, int year)
    {
        if (type.AnnualDays <= 0)
            return null;

        var balance = await _balances.Query()
            .FirstOrDefaultAsync(b => b.EmployeeId == employeeId && b.LeaveTypeId == type.Id && b.Year == year);
        if (balance is not null)
            return balance;

        var employee = await _unitOfWork.Repository<Employee>().QueryNoTracking()
            .Where(e => e.Id == employeeId)
            .Select(e => new { e.HireDate, e.BirthDate })
            .FirstAsync();

        balance = new LeaveBalance
        {
            EmployeeId = employeeId,
            LeaveTypeId = type.Id,
            Year = year,
            Entitled = LeaveCalculator.Entitlement(type.AnnualDays, type.SeniorAnnualDays, employee.HireDate, employee.BirthDate, year),
            Used = 0
        };
        await _balances.AddAsync(balance);
        return balance;
    }

    public Task<decimal> PendingDaysAsync(int employeeId, int leaveTypeId, int year, int? excludeRequestId = null)
        => _unitOfWork.Repository<LeaveRequest>().QueryNoTracking()
            .Where(r => r.EmployeeId == employeeId && r.LeaveTypeId == leaveTypeId && r.StartDate.Year == year
                && Waiting.Contains(r.Status) && r.Id != excludeRequestId)
            .SumAsync(r => r.Days);

    // For types without a balance, show how many days were taken
    private Task<decimal> UsedDaysAsync(int employeeId, int leaveTypeId, int year)
        => _unitOfWork.Repository<LeaveRequest>().QueryNoTracking()
            .Where(r => r.EmployeeId == employeeId && r.LeaveTypeId == leaveTypeId && r.StartDate.Year == year
                && r.Status == RequestStatus.Approved)
            .SumAsync(r => r.Days);
}
