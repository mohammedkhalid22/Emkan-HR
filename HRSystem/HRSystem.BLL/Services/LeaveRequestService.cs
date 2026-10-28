using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.DAL.Repositories;
using HRSystem.Domain.Entities;
using HRSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.BLL.Services;

public interface ILeaveRequestService
{
    Task<LeavePreviewDto> PreviewAsync(int employeeId, int leaveTypeId, DateOnly start, DateOnly end);
    Task<ServiceResult> SubmitAsync(int employeeId, CreateLeaveRequestDto dto, FileUpload? attachment);

    Task<List<LeaveRequestDto>> GetForEmployeeAsync(int employeeId, ApproverContext viewer);
    Task<List<LeaveRequestDto>> GetAwaitingAsync(ApproverContext approver);
    Task<int> CountAwaitingAsync(ApproverContext approver);
    Task<PagedResult<LeaveRequestDto>> GetPagedAsync(LeaveRequestFilterDto filter, ApproverContext viewer);
    Task<DocumentFile?> OpenAttachmentAsync(int requestId, ApproverContext viewer);

    Task<ServiceResult> ApproveAsync(int requestId, ApproverContext approver);
    Task<ServiceResult> RejectAsync(int requestId, ApproverContext approver, string reason);
    Task<ServiceResult> CancelAsync(int requestId, ApproverContext actor);
}

public class LeaveRequestService : ILeaveRequestService
{
    // Requests that still block their dates
    private static readonly RequestStatus[] Active = [RequestStatus.Pending, RequestStatus.ManagerApproved, RequestStatus.Approved];

    private const int MaxDaysBack = 30;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericRepository<LeaveRequest> _requests;
    private readonly IWorkCalendarService _calendar;
    private readonly ILeaveBalanceService _balances;
    private readonly IFileStorage _fileStorage;

    public LeaveRequestService(IUnitOfWork unitOfWork, IWorkCalendarService calendar,
        ILeaveBalanceService balances, IFileStorage fileStorage)
    {
        _unitOfWork = unitOfWork;
        _requests = unitOfWork.Repository<LeaveRequest>();
        _calendar = calendar;
        _balances = balances;
        _fileStorage = fileStorage;
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    // ---------- Submit ----------

    public async Task<LeavePreviewDto> PreviewAsync(int employeeId, int leaveTypeId, DateOnly start, DateOnly end)
    {
        var type = await _unitOfWork.Repository<LeaveType>().QueryNoTracking().FirstOrDefaultAsync(t => t.Id == leaveTypeId);
        if (type is null)
            return new LeavePreviewDto { Error = "اختار نوع الإجازة" };

        var preview = new LeavePreviewDto { RequiresAttachment = type.RequiresAttachment, IsLimited = type.AnnualDays > 0 };

        var dateError = ValidateDates(start, end);
        if (dateError is not null)
        {
            preview.Error = dateError;
            return preview;
        }

        preview.Days = await _calendar.CountWorkingDaysAsync(start, end);
        if (preview.IsLimited)
            preview.Available = await AvailableAsync(employeeId, type, start.Year, excludeRequestId: null);

        return preview;
    }

    public async Task<ServiceResult> SubmitAsync(int employeeId, CreateLeaveRequestDto dto, FileUpload? attachment)
    {
        var employee = await _unitOfWork.Repository<Employee>().QueryNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId);
        if (employee is null || employee.Status != EmployeeStatus.Active)
            return ServiceResult.Failure("لازم تكون موظف نشط عشان تقدم طلب إجازة");

        var type = await _unitOfWork.Repository<LeaveType>().QueryNoTracking().FirstOrDefaultAsync(t => t.Id == dto.LeaveTypeId);
        if (type is null)
            return ServiceResult.Failure("نوع الإجازة مش موجود", nameof(dto.LeaveTypeId));

        var start = dto.StartDate!.Value;
        var end = dto.EndDate!.Value;
        if (ValidateDates(start, end) is string dateError)
            return ServiceResult.Failure(dateError, nameof(dto.EndDate));
        if (start < employee.HireDate)
            return ServiceResult.Failure("تاريخ الإجازة قبل تاريخ التعيين", nameof(dto.StartDate));

        var days = await _calendar.CountWorkingDaysAsync(start, end);
        if (days == 0)
            return ServiceResult.Failure("الفترة دي كلها إجازات أسبوعية أو عطلات رسمية", nameof(dto.EndDate));

        var overlap = await _requests.QueryNoTracking()
            .Where(r => r.EmployeeId == employeeId && Active.Contains(r.Status) && r.StartDate <= end && r.EndDate >= start)
            .Select(r => new { r.StartDate, r.EndDate })
            .FirstOrDefaultAsync();
        if (overlap is not null)
            return ServiceResult.Failure($"عندك طلب تاني في نفس الفترة ({overlap.StartDate:yyyy-MM-dd} إلى {overlap.EndDate:yyyy-MM-dd})", nameof(dto.StartDate));

        if (type.RequiresAttachment && attachment is null)
            return ServiceResult.Failure($"إجازة {type.Name} محتاجة مرفق");
        if (attachment is not null && FileRules.Validate(attachment, FileRules.DocumentExtensions) is string fileError)
            return ServiceResult.Failure(fileError);

        if (type.AnnualDays > 0)
        {
            var available = await AvailableAsync(employeeId, type, start.Year, excludeRequestId: null);
            if (days > available)
                return ServiceResult.Failure($"رصيدك المتاح من {type.Name} {available:0.#} يوم والطلب {days} يوم");
        }

        var request = new LeaveRequest
        {
            EmployeeId = employeeId,
            LeaveTypeId = type.Id,
            StartDate = start,
            EndDate = end,
            Days = days,
            Reason = dto.Reason?.Trim(),
            Status = RequestStatus.Pending
        };

        if (attachment is not null)
            request.AttachmentPath = await _fileStorage.SaveAsync(attachment, $"employees/{employeeId}/leaves");

        await _requests.AddAsync(request);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    private static string? ValidateDates(DateOnly start, DateOnly end)
    {
        if (end < start)
            return "تاريخ النهاية لازم يكون بعد تاريخ البداية";
        if (start.Year != end.Year)
            return "الإجازة لازم تبقى في نفس السنة. قسّمها على طلبين";
        if (start < Today.AddDays(-MaxDaysBack))
            return $"مينفعش تقدم إجازة بدأت من أكتر من {MaxDaysBack} يوم";
        if (end > Today.AddYears(1))
            return "الإجازة بعيدة أوي في المستقبل";
        return null;
    }

    private async Task<decimal> AvailableAsync(int employeeId, LeaveType type, int year, int? excludeRequestId)
    {
        var balance = await _balances.EnsureAsync(employeeId, type, year);
        await _unitOfWork.SaveChangesAsync();
        if (balance is null)
            return decimal.MaxValue;

        var pending = await _balances.PendingDaysAsync(employeeId, type.Id, year, excludeRequestId);
        return balance.Entitled - balance.Used - pending;
    }

    // ---------- Queries ----------

    public async Task<List<LeaveRequestDto>> GetForEmployeeAsync(int employeeId, ApproverContext viewer)
    {
        var list = await Project(_requests.QueryNoTracking().Where(r => r.EmployeeId == employeeId))
            .OrderByDescending(r => r.StartDate)
            .ToListAsync();
        list.ForEach(r => FillActions(r, viewer));
        return list;
    }

    public async Task<List<LeaveRequestDto>> GetAwaitingAsync(ApproverContext approver)
    {
        var list = await Project(AwaitingQuery(approver)).OrderBy(r => r.StartDate).ToListAsync();
        list.ForEach(r => FillActions(r, approver));
        return list;
    }

    public Task<int> CountAwaitingAsync(ApproverContext approver) => AwaitingQuery(approver).CountAsync();

    // Manager: their direct reports at the first step. HR: second step, or first step for people without a manager.
    private IQueryable<LeaveRequest> AwaitingQuery(ApproverContext approver)
    {
        var own = approver.EmployeeId;
        return _requests.QueryNoTracking().Where(r =>
            r.EmployeeId != own &&
            ((approver.IsManager && own != null && r.Status == RequestStatus.Pending && r.Employee.ManagerId == own)
             || (approver.IsHr && (r.Status == RequestStatus.ManagerApproved
                                   || (r.Status == RequestStatus.Pending && r.Employee.ManagerId == null)))));
    }

    public async Task<PagedResult<LeaveRequestDto>> GetPagedAsync(LeaveRequestFilterDto filter, ApproverContext viewer)
    {
        var query = _requests.QueryNoTracking();

        if (filter.Status is RequestStatus status)
            query = query.Where(r => r.Status == status);
        if (filter.Year is int year)
            query = query.Where(r => r.StartDate.Year == year);
        if (filter.DepartmentId is int departmentId)
            query = query.Where(r => r.Employee.DepartmentId == departmentId);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(r => r.Employee.FullNameAr.Contains(term) || r.Employee.Code.Contains(term));
        }

        var page = Math.Max(1, filter.Page);
        var total = await query.CountAsync();
        var items = await Project(query.OrderByDescending(r => r.StartDate))
            .Skip((page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();
        items.ForEach(r => FillActions(r, viewer));

        return new PagedResult<LeaveRequestDto> { Items = items, TotalCount = total, Page = page, PageSize = filter.PageSize };
    }

    public async Task<DocumentFile?> OpenAttachmentAsync(int requestId, ApproverContext viewer)
    {
        var request = await _requests.QueryNoTracking()
            .Where(r => r.Id == requestId)
            .Select(r => new { r.AttachmentPath, r.EmployeeId, r.Employee.ManagerId })
            .FirstOrDefaultAsync();

        var allowed = request is not null && (viewer.IsHr || request.EmployeeId == viewer.EmployeeId
            || (viewer.IsManager && viewer.EmployeeId != null && request.ManagerId == viewer.EmployeeId));
        if (!allowed || request!.AttachmentPath is null)
            return null;

        var stream = _fileStorage.Open(request.AttachmentPath);
        return stream is null ? null : new DocumentFile(stream,
            EmployeeDocumentService.OriginalFileName(request.AttachmentPath), FileRules.ContentTypeFor(request.AttachmentPath));
    }

    private static IQueryable<LeaveRequestDto> Project(IQueryable<LeaveRequest> query)
        => query.Select(r => new LeaveRequestDto
        {
            Id = r.Id,
            EmployeeId = r.EmployeeId,
            EmployeeCode = r.Employee.Code,
            EmployeeName = r.Employee.FullNameAr,
            DepartmentName = r.Employee.Department.Name,
            EmployeeManagerId = r.Employee.ManagerId,
            LeaveTypeName = r.LeaveType.Name,
            StartDate = r.StartDate,
            EndDate = r.EndDate,
            Days = r.Days,
            Reason = r.Reason,
            Status = r.Status,
            RejectionReason = r.RejectionReason,
            HasAttachment = r.AttachmentPath != null,
            CreatedAt = r.CreatedAt,
            ManagerActionAt = r.ManagerActionAt,
            HrActionAt = r.HrActionAt
        });

    private static void FillActions(LeaveRequestDto r, ApproverContext viewer)
    {
        r.CanApprove = StepFor(r.Status, r.EmployeeId, r.EmployeeManagerId, viewer) is not null;
        r.CanCancel = CanCancel(r.Status, r.StartDate, r.EmployeeId, viewer);
    }

    // ---------- Decisions ----------

    private static ApprovalStep? StepFor(RequestStatus status, int employeeId, int? employeeManagerId, ApproverContext approver)
        => ApprovalRules.StepFor(status, employeeId, employeeManagerId, approver);

    private static bool CanCancel(RequestStatus status, DateOnly start, int employeeId, ApproverContext actor)
        => ApprovalRules.CanCancel(status, hasStarted: start <= Today, employeeId, actor);

    public async Task<ServiceResult> ApproveAsync(int requestId, ApproverContext approver)
    {
        var request = await _requests.Query().Include(r => r.Employee).Include(r => r.LeaveType)
            .FirstOrDefaultAsync(r => r.Id == requestId);
        if (request is null)
            return ServiceResult.Failure("الطلب مش موجود");

        var step = StepFor(request.Status, request.EmployeeId, request.Employee.ManagerId, approver);
        if (step is null)
            return ServiceResult.Failure("الطلب ده مش في انتظار موافقتك");

        var now = DateTime.UtcNow;
        if (step == ApprovalStep.Manager)
        {
            request.Status = RequestStatus.ManagerApproved;
            request.ManagerActionBy = approver.UserId;
            request.ManagerActionAt = now;
        }
        else
        {
            // Final approval: this is where the days leave the balance
            var balance = await _balances.EnsureAsync(request.EmployeeId, request.LeaveType, request.StartDate.Year);
            if (balance is not null)
            {
                if (balance.Used + request.Days > balance.Entitled)
                    return ServiceResult.Failure($"رصيد الموظف مش كفاية ({balance.Entitled - balance.Used:0.#} يوم متبقي)");
                balance.Used += request.Days;
            }

            request.Status = RequestStatus.Approved;
            request.HrActionBy = approver.UserId;
            request.HrActionAt = now;
        }

        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> RejectAsync(int requestId, ApproverContext approver, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return ServiceResult.Failure("اكتب سبب الرفض");

        var request = await _requests.Query().Include(r => r.Employee).FirstOrDefaultAsync(r => r.Id == requestId);
        if (request is null)
            return ServiceResult.Failure("الطلب مش موجود");

        var step = StepFor(request.Status, request.EmployeeId, request.Employee.ManagerId, approver);
        if (step is null)
            return ServiceResult.Failure("الطلب ده مش في انتظار موافقتك");

        request.Status = RequestStatus.Rejected;
        request.RejectionReason = reason.Trim();
        if (step == ApprovalStep.Manager)
        {
            request.ManagerActionBy = approver.UserId;
            request.ManagerActionAt = DateTime.UtcNow;
        }
        else
        {
            request.HrActionBy = approver.UserId;
            request.HrActionAt = DateTime.UtcNow;
        }

        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> CancelAsync(int requestId, ApproverContext actor)
    {
        var request = await _requests.Query().Include(r => r.LeaveType).FirstOrDefaultAsync(r => r.Id == requestId);
        if (request is null)
            return ServiceResult.Failure("الطلب مش موجود");

        if (!CanCancel(request.Status, request.StartDate, request.EmployeeId, actor))
            return ServiceResult.Failure(request.Status == RequestStatus.Approved
                ? "مينفعش تلغي إجازة بدأت بالفعل"
                : "الطلب ده مينفعش يتلغي");

        // An approved leave that hasn't started gives its days back
        if (request.Status == RequestStatus.Approved)
        {
            var balance = await _balances.EnsureAsync(request.EmployeeId, request.LeaveType, request.StartDate.Year);
            if (balance is not null)
                balance.Used = Math.Max(0, balance.Used - request.Days);
        }

        request.Status = RequestStatus.Cancelled;
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }
}
