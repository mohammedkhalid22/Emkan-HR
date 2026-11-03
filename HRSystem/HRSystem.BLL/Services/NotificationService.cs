using System.Net;
using HRSystem.BLL.Common;
using HRSystem.DAL.Repositories;
using HRSystem.Domain.Entities;
using HRSystem.Domain.Enums;
using HRSystem.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HRSystem.BLL.Services;

public record NotificationDto(int Id, NotificationType Type, string Title, string? Body, string? Url, bool IsRead, DateTime CreatedAt);

public interface INotificationService
{
    // ---------- The bell ----------
    Task<int> UnreadCountAsync(string userId);
    Task<List<NotificationDto>> GetLatestAsync(string userId, int take);
    Task<PagedResult<NotificationDto>> GetPagedAsync(string userId, int page, int pageSize);

    // Marks it read and returns where it points
    Task<string?> OpenAsync(string userId, int id);
    Task MarkAllReadAsync(string userId);

    // ---------- Events ----------
    // `what` reads as the request itself: "إجازة سنوية من 16 نوفمبر لـ 18 نوفمبر"
    Task RequestSubmittedAsync(int employeeId, string what);
    Task RequestManagerApprovedAsync(int employeeId, string what);
    Task RequestDecidedAsync(int employeeId, string what, bool approved, string? reason, string url);
    Task PayslipsReadyAsync(int runId);

    // Contracts and documents about to end, birthdays, work anniversaries; returns how many were sent
    Task<int> SendDailyRemindersAsync(DateOnly today);
}

public class NotificationService : INotificationService
{
    private const string AwaitingUrl = "/LeaveRequests/Awaiting";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericRepository<Notification> _notifications;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IEmailSender _email;
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(IUnitOfWork unitOfWork, UserManager<ApplicationUser> users, IEmailSender email,
        IOptions<EmailOptions> emailOptions, ILogger<NotificationService> logger)
    {
        _unitOfWork = unitOfWork;
        _notifications = unitOfWork.Repository<Notification>();
        _users = users;
        _email = email;
        _emailOptions = emailOptions.Value;
        _logger = logger;
    }

    // ---------- The bell ----------

    public Task<int> UnreadCountAsync(string userId)
        => _notifications.QueryNoTracking().CountAsync(n => n.UserId == userId && !n.IsRead);

    public Task<List<NotificationDto>> GetLatestAsync(string userId, int take)
        => Project(_notifications.QueryNoTracking().Where(n => n.UserId == userId)).Take(take).ToListAsync();

    public async Task<PagedResult<NotificationDto>> GetPagedAsync(string userId, int page, int pageSize)
    {
        var query = _notifications.QueryNoTracking().Where(n => n.UserId == userId);
        page = Math.Max(1, page);
        return new PagedResult<NotificationDto>
        {
            Items = await Project(query).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(),
            TotalCount = await query.CountAsync(),
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<string?> OpenAsync(string userId, int id)
    {
        // Only the owner can open (and so mark) a notification
        var notification = await _notifications.Query().FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        if (notification is null)
            return null;

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
        }
        return notification.Url;
    }

    public async Task MarkAllReadAsync(string userId)
    {
        var unread = await _notifications.Query().Where(n => n.UserId == userId && !n.IsRead).ToListAsync();
        var now = DateTime.UtcNow;
        unread.ForEach(n => { n.IsRead = true; n.ReadAt = now; });
        await _unitOfWork.SaveChangesAsync();
    }

    private static IQueryable<NotificationDto> Project(IQueryable<Notification> query)
        => query.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Title, n.Body, n.Url, n.IsRead, n.CreatedAt));

    // ---------- Events ----------

    public async Task RequestSubmittedAsync(int employeeId, string what)
    {
        var employee = await EmployeeAsync(employeeId);
        if (employee is null)
            return;

        // The direct manager decides first; someone without one goes straight to HR
        var recipients = employee.ManagerUserId is not null && employee.ManagerActive
            ? [employee.ManagerUserId]
            : await HrUserIdsAsync();

        await SendAsync(recipients.Where(id => id != employee.UserId), NotificationType.RequestWaiting,
            $"طلب جديد من {employee.Name}", what, AwaitingUrl);
    }

    public async Task RequestManagerApprovedAsync(int employeeId, string what)
    {
        var employee = await EmployeeAsync(employeeId);
        if (employee is null)
            return;

        var hr = await HrUserIdsAsync();
        await SendAsync(hr.Where(id => id != employee.UserId), NotificationType.RequestWaiting,
            $"طلب {employee.Name} مستني اعتمادك", $"{what} · المدير وافق", AwaitingUrl);
    }

    public async Task RequestDecidedAsync(int employeeId, string what, bool approved, string? reason, string url)
    {
        var employee = await EmployeeAsync(employeeId);
        if (employee?.UserId is null)
            return;

        await SendAsync([employee.UserId],
            approved ? NotificationType.RequestApproved : NotificationType.RequestRejected,
            approved ? "طلبك اتوافق عليه" : "طلبك اترفض",
            approved ? what : $"{what} · السبب: {reason}",
            url);
    }

    public async Task PayslipsReadyAsync(int runId)
    {
        var run = await _unitOfWork.Repository<PayrollRun>().QueryNoTracking()
            .Where(r => r.Id == runId)
            .Select(r => new { r.Year, r.Month, Users = r.Items.Select(i => i.Employee.UserId).Where(u => u != null).ToList() })
            .FirstOrDefaultAsync();
        if (run is null)
            return;

        await SendAsync(run.Users!, NotificationType.Payslip,
            $"كشف مرتب {HrAlerts.MonthName(run.Month)} {run.Year} جاهز", "تقدر تشوفه وتنزّله PDF", "/Payslips/Mine");
    }

    // ---------- Daily reminders ----------

    public async Task<int> SendDailyRemindersAsync(DateOnly today)
    {
        var hr = await HrUserIdsAsync();
        var planned = new List<Notification>();
        void Plan(string userId, NotificationType type, string title, string body, string url, string key)
            => planned.Add(new Notification { UserId = userId, Type = type, Title = title, Body = body, Url = url, Key = key });

        var employees = await _unitOfWork.Repository<Employee>().QueryNoTracking()
            .Where(e => e.Status == EmployeeStatus.Active)
            .Select(e => new { e.Id, e.FullNameAr, e.UserId, e.BirthDate, e.HireDate, e.ContractType, e.ContractEndDate })
            .ToListAsync();

        foreach (var e in employees)
        {
            var profile = $"/Employees/Details/{e.Id}";

            if (e.ContractEndDate is DateOnly end && HrAlerts.IsWithin(end, today, HrAlerts.ContractWarningDays))
            {
                var days = end.DayNumber - today.DayNumber;
                foreach (var u in hr)
                    Plan(u, NotificationType.ContractEnding, $"عقد {e.FullNameAr} بيخلص {(days == 0 ? "النهارده" : $"بعد {days} يوم")}",
                        $"العقد المؤقت بينتهي {HrAlerts.Day(end)}. جدّده أو حضّر لإنهاء الخدمة", profile, $"contract:{e.Id}:{end:yyyyMMdd}");
            }

            if (HrAlerts.IsBirthday(e.BirthDate, today))
            {
                foreach (var u in hr.Where(u => u != e.UserId))
                    Plan(u, NotificationType.Birthday, $"النهارده عيد ميلاد {e.FullNameAr} 🎂", "قول له كل سنة وإنت طيب", profile, $"birthday:{e.Id}:{today.Year}");
                if (e.UserId is not null)
                    Plan(e.UserId, NotificationType.Birthday, "كل سنة وإنت طيب 🎂", "إمكان HR بيتمنالك سنة حلوة", "/Portal", $"birthday:{e.Id}:{today.Year}");
            }

            if (HrAlerts.AnniversaryYears(e.HireDate, today) is int years)
            {
                foreach (var u in hr.Where(u => u != e.UserId))
                    Plan(u, NotificationType.WorkAnniversary, $"{e.FullNameAr} كمّل {years} {(years == 1 ? "سنة" : "سنين")} في الشركة", $"اتعيّن {HrAlerts.Day(e.HireDate)} {e.HireDate.Year}", profile, $"anniversary:{e.Id}:{today.Year}");
                if (e.UserId is not null)
                    Plan(e.UserId, NotificationType.WorkAnniversary, $"مبروك! كمّلت {years} {(years == 1 ? "سنة" : "سنين")} معانا", "شكراً على مجهودك", "/Portal", $"anniversary:{e.Id}:{today.Year}");
            }
        }

        var documents = await _unitOfWork.Repository<EmployeeDocument>().QueryNoTracking()
            .Where(d => d.ExpiryDate != null && d.Employee.Status == EmployeeStatus.Active)
            .Select(d => new { d.Id, d.EmployeeId, d.DocumentType, d.ExpiryDate, d.Employee.FullNameAr })
            .ToListAsync();
        foreach (var d in documents.Where(d => HrAlerts.IsWithin(d.ExpiryDate!.Value, today, HrAlerts.DocumentWarningDays)))
        {
            foreach (var u in hr)
                Plan(u, NotificationType.DocumentExpiring, $"{d.DocumentType} بتاع {d.FullNameAr} بيخلص قريب",
                    $"بينتهي {HrAlerts.Day(d.ExpiryDate!.Value)}. اطلب منه نسخة جديدة", $"/Employees/Details/{d.EmployeeId}", $"document:{d.Id}:{d.ExpiryDate:yyyyMMdd}");
        }

        if (planned.Count == 0)
            return 0;

        // Skip the reminders already sent on an earlier run
        var keys = planned.Select(p => p.Key!).Distinct().ToList();
        var sent = await _notifications.QueryNoTracking().IgnoreQueryFilters()
            .Where(n => n.Key != null && keys.Contains(n.Key))
            .Select(n => new { n.UserId, n.Key })
            .ToListAsync();
        var fresh = planned.Where(p => !sent.Any(s => s.UserId == p.UserId && s.Key == p.Key)).ToList();

        await SaveAndEmailAsync(fresh);
        return fresh.Count;
    }

    // ---------- Helpers ----------

    private record EmployeeInfo(string Name, string? UserId, string? ManagerUserId, bool ManagerActive);

    private Task<EmployeeInfo?> EmployeeAsync(int employeeId)
        => _unitOfWork.Repository<Employee>().QueryNoTracking()
            .Where(e => e.Id == employeeId)
            .Select(e => new EmployeeInfo(e.FullNameAr, e.UserId, e.Manager != null ? e.Manager.UserId : null,
                e.Manager != null && e.Manager.Status == EmployeeStatus.Active))
            .FirstOrDefaultAsync();

    // HR and admins who can still sign in
    private async Task<List<string>> HrUserIdsAsync()
    {
        var people = (await _users.GetUsersInRoleAsync(Roles.HR)).Concat(await _users.GetUsersInRoleAsync(Roles.Admin));
        return people.Where(u => u.IsActive).Select(u => u.Id).Distinct().ToList();
    }

    private Task SendAsync(IEnumerable<string> userIds, NotificationType type, string title, string? body, string url)
        => SaveAndEmailAsync(userIds.Distinct()
            .Select(id => new Notification { UserId = id, Type = type, Title = title, Body = body, Url = url })
            .ToList());

    private async Task SaveAndEmailAsync(List<Notification> notifications)
    {
        if (notifications.Count == 0)
            return;

        foreach (var n in notifications)
            await _notifications.AddAsync(n);
        await _unitOfWork.SaveChangesAsync();

        if (!_email.IsConfigured)
            return;

        // Email is a copy of the bell: a failure never undoes the notification
        var userIds = notifications.Select(n => n.UserId).Distinct().ToList();
        var emails = await _users.Users.Where(u => userIds.Contains(u.Id) && u.Email != null && u.IsActive)
            .ToDictionaryAsync(u => u.Id, u => u.Email!);
        foreach (var n in notifications.Where(n => emails.ContainsKey(n.UserId)))
        {
            try
            {
                await _email.SendAsync(emails[n.UserId], n.Title, EmailBody(n));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not email notification {Id}", n.Id);
            }
        }
    }

    private string EmailBody(Notification n)
    {
        var link = _emailOptions.BaseUrl is { Length: > 0 } baseUrl && n.Url is not null
            ? $"<p><a href=\"{WebUtility.HtmlEncode(baseUrl.TrimEnd('/') + n.Url)}\">افتح في إمكان HR</a></p>"
            : "";
        return $"""
            <div dir="rtl" style="font-family:Tahoma,Arial,sans-serif;font-size:15px;color:#0f2a4a">
              <h2 style="font-size:18px">{WebUtility.HtmlEncode(n.Title)}</h2>
              <p>{WebUtility.HtmlEncode(n.Body ?? "")}</p>
              {link}
              <p style="color:#6a7b8f;font-size:12px">رسالة تلقائية من إمكان HR</p>
            </div>
            """;
    }
}
