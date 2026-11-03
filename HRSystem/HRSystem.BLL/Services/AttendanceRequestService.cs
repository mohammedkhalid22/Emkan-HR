using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.DAL.Repositories;
using HRSystem.Domain.Entities;
using HRSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.BLL.Services;

// Permissions (leave part of the day) and missions (work outside the office), approved like leaves: manager then HR
public interface IAttendanceRequestService
{
    Task<ServiceResult> SubmitAsync(int employeeId, CreateAttendanceRequestDto dto);
    Task<List<AttendanceRequestDto>> GetForEmployeeAsync(int employeeId, ApproverContext viewer);
    Task<List<AttendanceRequestDto>> GetAwaitingAsync(ApproverContext approver);
    Task<int> CountAwaitingAsync(ApproverContext approver);

    Task<ServiceResult> ApproveAsync(int requestId, ApproverContext approver);
    Task<ServiceResult> RejectAsync(int requestId, ApproverContext approver, string reason);
    Task<ServiceResult> CancelAsync(int requestId, ApproverContext actor);
}

public class AttendanceRequestService : IAttendanceRequestService
{
    private static readonly RequestStatus[] Active = [RequestStatus.Pending, RequestStatus.ManagerApproved, RequestStatus.Approved];

    private const int MaxPermissionHours = 4;
    private const int MaxDaysBack = 7;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericRepository<AttendanceRequest> _requests;
    private readonly IAttendanceService _attendance;
    private readonly INotificationService _notifications;

    private const string MineUrl = "/AttendanceRequests/Mine";

    public AttendanceRequestService(IUnitOfWork unitOfWork, IAttendanceService attendance, INotificationService notifications)
    {
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _requests = unitOfWork.Repository<AttendanceRequest>();
        _attendance = attendance;
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    public async Task<ServiceResult> SubmitAsync(int employeeId, CreateAttendanceRequestDto dto)
    {
        var employee = await _unitOfWork.Repository<Employee>().QueryNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId);
        if (employee is null || employee.Status != EmployeeStatus.Active)
            return ServiceResult.Failure("لازم تكون موظف نشط عشان تقدم طلب");

        var date = dto.Date!.Value;
        var from = dto.FromTime!.Value;
        var to = dto.ToTime!.Value;

        if (to <= from)
            return ServiceResult.Failure("وقت النهاية لازم يكون بعد وقت البداية", nameof(dto.ToTime));
        if (date < Today.AddDays(-MaxDaysBack))
            return ServiceResult.Failure($"مينفعش تقدم طلب ليوم فات عليه أكتر من {MaxDaysBack} أيام", nameof(dto.Date));
        if (date > Today.AddMonths(2))
            return ServiceResult.Failure("التاريخ بعيد أوي في المستقبل", nameof(dto.Date));
        if (dto.Type == AttendanceRequestType.Permission && (to.ToTimeSpan() - from.ToTimeSpan()).TotalHours > MaxPermissionHours)
            return ServiceResult.Failure($"الإذن أقصاه {MaxPermissionHours} ساعات. لو أكتر قدّم إجازة", nameof(dto.ToTime));

        var onLeave = await _unitOfWork.Repository<LeaveRequest>().QueryNoTracking()
            .AnyAsync(l => l.EmployeeId == employeeId && Active.Contains(l.Status) && l.StartDate <= date && l.EndDate >= date);
        if (onLeave)
            return ServiceResult.Failure("عندك طلب إجازة في اليوم ده", nameof(dto.Date));

        var overlap = await _requests.QueryNoTracking()
            .AnyAsync(r => r.EmployeeId == employeeId && r.Date == date && Active.Contains(r.Status) && r.FromTime < to && r.ToTime > from);
        if (overlap)
            return ServiceResult.Failure("عندك طلب تاني في نفس الوقت", nameof(dto.FromTime));

        await _requests.AddAsync(new AttendanceRequest
        {
            EmployeeId = employeeId,
            Type = dto.Type!.Value,
            Date = date,
            FromTime = from,
            ToTime = to,
            Reason = dto.Reason.Trim(),
            Status = RequestStatus.Pending
        });
        await _unitOfWork.SaveChangesAsync();
        await _notifications.RequestSubmittedAsync(employeeId, Describe(dto.Type!.Value, date, from, to));
        return ServiceResult.Success();
    }

    // "إذن يوم 5 نوفمبر من 16:00 لـ 18:00"
    private static string Describe(AttendanceRequestType type, DateOnly date, TimeOnly from, TimeOnly to)
        => $"{(type == AttendanceRequestType.Permission ? "إذن" : "مأمورية")} يوم {HrAlerts.Day(date)} من {from:HH:mm} لـ {to:HH:mm}";

    public async Task<List<AttendanceRequestDto>> GetForEmployeeAsync(int employeeId, ApproverContext viewer)
    {
        var list = await Project(_requests.QueryNoTracking().Where(r => r.EmployeeId == employeeId))
            .OrderByDescending(r => r.Date).ThenByDescending(r => r.FromTime)
            .ToListAsync();
        list.ForEach(r => FillActions(r, viewer));
        return list;
    }

    public async Task<List<AttendanceRequestDto>> GetAwaitingAsync(ApproverContext approver)
    {
        var list = await Project(AwaitingQuery(approver)).OrderBy(r => r.Date).ToListAsync();
        list.ForEach(r => FillActions(r, approver));
        return list;
    }

    public Task<int> CountAwaitingAsync(ApproverContext approver) => AwaitingQuery(approver).CountAsync();

    private IQueryable<AttendanceRequest> AwaitingQuery(ApproverContext approver)
    {
        var own = approver.EmployeeId;
        return _requests.QueryNoTracking().Where(r =>
            r.EmployeeId != own &&
            ((approver.IsManager && own != null && r.Status == RequestStatus.Pending && r.Employee.ManagerId == own)
             || (approver.IsHr && (r.Status == RequestStatus.ManagerApproved
                                   || (r.Status == RequestStatus.Pending && r.Employee.ManagerId == null)))));
    }

    private static IQueryable<AttendanceRequestDto> Project(IQueryable<AttendanceRequest> query)
        => query.Select(r => new AttendanceRequestDto
        {
            Id = r.Id,
            EmployeeId = r.EmployeeId,
            EmployeeName = r.Employee.FullNameAr,
            DepartmentName = r.Employee.Department.Name,
            EmployeeManagerId = r.Employee.ManagerId,
            Type = r.Type,
            Date = r.Date,
            FromTime = r.FromTime,
            ToTime = r.ToTime,
            Reason = r.Reason,
            Status = r.Status,
            RejectionReason = r.RejectionReason
        });

    private static void FillActions(AttendanceRequestDto r, ApproverContext viewer)
    {
        r.CanApprove = ApprovalRules.StepFor(r.Status, r.EmployeeId, r.EmployeeManagerId, viewer) is not null;
        r.CanCancel = ApprovalRules.CanCancel(r.Status, hasStarted: r.Date < Today, r.EmployeeId, viewer);
    }

    public async Task<ServiceResult> ApproveAsync(int requestId, ApproverContext approver)
    {
        var request = await _requests.Query().Include(r => r.Employee).FirstOrDefaultAsync(r => r.Id == requestId);
        if (request is null)
            return ServiceResult.Failure("الطلب مش موجود");

        var step = ApprovalRules.StepFor(request.Status, request.EmployeeId, request.Employee.ManagerId, approver);
        if (step is null)
            return ServiceResult.Failure("الطلب ده مش في انتظار موافقتك");

        request.Status = step == ApprovalStep.Manager ? RequestStatus.ManagerApproved : RequestStatus.Approved;
        await _unitOfWork.SaveChangesAsync();

        // A permission approved after the employee already checked in removes the late / early minutes
        if (request.Status == RequestStatus.Approved)
            await _attendance.RecalculateAsync(request.EmployeeId, request.Date);

        var what = Describe(request.Type, request.Date, request.FromTime, request.ToTime);
        if (request.Status == RequestStatus.ManagerApproved)
            await _notifications.RequestManagerApprovedAsync(request.EmployeeId, what);
        else
            await _notifications.RequestDecidedAsync(request.EmployeeId, what, approved: true, reason: null, MineUrl);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> RejectAsync(int requestId, ApproverContext approver, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return ServiceResult.Failure("اكتب سبب الرفض");

        var request = await _requests.Query().Include(r => r.Employee).FirstOrDefaultAsync(r => r.Id == requestId);
        if (request is null)
            return ServiceResult.Failure("الطلب مش موجود");

        if (ApprovalRules.StepFor(request.Status, request.EmployeeId, request.Employee.ManagerId, approver) is null)
            return ServiceResult.Failure("الطلب ده مش في انتظار موافقتك");

        request.Status = RequestStatus.Rejected;
        request.RejectionReason = reason.Trim();
        await _unitOfWork.SaveChangesAsync();
        await _notifications.RequestDecidedAsync(request.EmployeeId,
            Describe(request.Type, request.Date, request.FromTime, request.ToTime), approved: false, request.RejectionReason, MineUrl);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> CancelAsync(int requestId, ApproverContext actor)
    {
        var request = await _requests.GetByIdAsync(requestId);
        if (request is null)
            return ServiceResult.Failure("الطلب مش موجود");

        if (!ApprovalRules.CanCancel(request.Status, hasStarted: request.Date < Today, request.EmployeeId, actor))
            return ServiceResult.Failure("الطلب ده مينفعش يتلغي");

        var wasApproved = request.Status == RequestStatus.Approved;
        request.Status = RequestStatus.Cancelled;
        await _unitOfWork.SaveChangesAsync();

        if (wasApproved)
            await _attendance.RecalculateAsync(request.EmployeeId, request.Date);

        return ServiceResult.Success();
    }
}
