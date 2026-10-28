using HRSystem.DAL.Repositories;
using HRSystem.Domain.Entities;
using HRSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.BLL.Services;

public class HrDashboardDto
{
    public int ActiveEmployees { get; set; }
    public int PresentToday { get; set; }
    public int OnLeaveToday { get; set; }
    public int PendingLeaveRequests { get; set; }
    public List<ExpiringItemDto> ExpiringContracts { get; set; } = [];
    public List<ExpiringItemDto> ExpiringDocuments { get; set; } = [];
    public List<(string Department, int Count)> EmployeesByDepartment { get; set; } = [];
}

public record ExpiringItemDto(int EmployeeId, string EmployeeName, string What, DateOnly Date);

public interface IDashboardService
{
    Task<HrDashboardDto> GetHrDashboardAsync();
}

public class DashboardService : IDashboardService
{
    private const int ExpiryWindowDays = 30;

    private readonly IUnitOfWork _unitOfWork;

    public DashboardService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<HrDashboardDto> GetHrDashboardAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var soon = today.AddDays(ExpiryWindowDays);
        var employees = _unitOfWork.Repository<Employee>().QueryNoTracking().Where(e => e.Status == EmployeeStatus.Active);

        var byDepartment = await employees
            .GroupBy(e => e.Department.Name)
            .Select(g => new { g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        return new HrDashboardDto
        {
            ActiveEmployees = await employees.CountAsync(),
            PresentToday = await _unitOfWork.Repository<AttendanceRecord>().QueryNoTracking()
                .CountAsync(r => r.Date == today && r.CheckIn != null),
            OnLeaveToday = await _unitOfWork.Repository<LeaveRequest>().QueryNoTracking()
                .CountAsync(l => l.Status == RequestStatus.Approved && l.StartDate <= today && l.EndDate >= today),
            PendingLeaveRequests = await _unitOfWork.Repository<LeaveRequest>().QueryNoTracking()
                .CountAsync(l => l.Status == RequestStatus.Pending || l.Status == RequestStatus.ManagerApproved),
            ExpiringContracts = await employees
                .Where(e => e.ContractEndDate != null && e.ContractEndDate <= soon)
                .OrderBy(e => e.ContractEndDate)
                .Select(e => new ExpiringItemDto(e.Id, e.FullNameAr, "انتهاء العقد", e.ContractEndDate!.Value))
                .ToListAsync(),
            ExpiringDocuments = await _unitOfWork.Repository<EmployeeDocument>().QueryNoTracking()
                .Where(d => d.ExpiryDate != null && d.ExpiryDate <= soon && d.Employee.Status == EmployeeStatus.Active)
                .OrderBy(d => d.ExpiryDate)
                .Select(d => new ExpiringItemDto(d.EmployeeId, d.Employee.FullNameAr, d.DocumentType, d.ExpiryDate!.Value))
                .ToListAsync(),
            EmployeesByDepartment = byDepartment.Select(x => (x.Key, x.Count)).ToList()
        };
    }
}
