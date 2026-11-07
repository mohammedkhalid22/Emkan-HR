using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Cryptography;
using HRSystem.BLL.Common;
using HRSystem.BLL.Devices;
using HRSystem.BLL.DTOs;
using HRSystem.DAL.Data;
using HRSystem.DAL.Repositories;
using HRSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRSystem.BLL.Services;

public class DeviceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string IpAddress { get; set; } = null!;
    public int Port { get; set; }
    public bool IsEnabled { get; set; }
    public bool PulledByServer { get; set; }
    public string ApiKey { get; set; } = null!;
    public DateTime? LastSyncAt { get; set; }
    public bool? LastSyncSucceeded { get; set; }
    public string? LastSyncMessage { get; set; }
}

public class SaveDeviceDto
{
    public int Id { get; set; }

    [Display(Name = "اسم الجهاز")]
    [Required(ErrorMessage = "{0} مطلوب")]
    [StringLength(100, ErrorMessage = "{0} لازم يكون {1} حرف أو أقل")]
    public string Name { get; set; } = null!;

    [Display(Name = "عنوان IP")]
    [Required(ErrorMessage = "{0} مطلوب")]
    [StringLength(100)]
    public string IpAddress { get; set; } = null!;

    [Display(Name = "البورت")]
    [Range(1, 65535, ErrorMessage = "{0} بين {1} و {2}")]
    public int Port { get; set; } = 4370;

    [Display(Name = "Comm Key")]
    [Range(0, 999999, ErrorMessage = "{0} رقم من 0 لـ {2}")]
    public int CommKey { get; set; }

    [Display(Name = "شغال")]
    public bool IsEnabled { get; set; } = true;

    [Display(Name = "السيرفر بيوصل للجهاز مباشرة")]
    public bool PulledByServer { get; set; }
}

public record PunchSyncResult(int Received, int New, int Unmatched, int DaysUpdated, List<string> Problems);

// Fingerprint devices: the server pulls their log over IP, or the company-side agent pushes it here.
// Raw punches are kept once each; a day's first punch is the check-in and its last the check-out.
public interface IDeviceService
{
    Task<List<DeviceDto>> GetAllAsync();
    Task<SaveDeviceDto?> GetForEditAsync(int id);
    Task<ServiceResult> CreateAsync(SaveDeviceDto dto);
    Task<ServiceResult> UpdateAsync(SaveDeviceDto dto);
    Task<ServiceResult> DeleteAsync(int id);
    Task<ServiceResult> NewApiKeyAsync(int id);

    // Connects from this server and reads the log
    Task<ServiceResult<PunchSyncResult>> PullAsync(int deviceId, CancellationToken cancellationToken = default);

    // Punches sent by the agent; the key says which device
    Task<ServiceResult<PunchSyncResult>> ReceiveAsync(string apiKey, IReadOnlyList<DevicePunch> punches);

    Task<List<int>> PulledDeviceIdsAsync();
}

public class DeviceService : IDeviceService
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

    private readonly IUnitOfWork _unitOfWork;
    private readonly HRDbContext _context;
    private readonly IGenericRepository<AttendanceDevice> _devices;
    private readonly IAttendanceService _attendance;
    private readonly ILogger<DeviceService> _logger;

    public DeviceService(IUnitOfWork unitOfWork, HRDbContext context, IAttendanceService attendance, ILogger<DeviceService> logger)
    {
        _unitOfWork = unitOfWork;
        _context = context;
        _devices = unitOfWork.Repository<AttendanceDevice>();
        _attendance = attendance;
        _logger = logger;
    }

    public Task<List<DeviceDto>> GetAllAsync()
        => _devices.QueryNoTracking().OrderBy(d => d.Name)
            .Select(d => new DeviceDto
            {
                Id = d.Id, Name = d.Name, IpAddress = d.IpAddress, Port = d.Port, IsEnabled = d.IsEnabled,
                PulledByServer = d.PulledByServer, ApiKey = d.ApiKey, LastSyncAt = d.LastSyncAt,
                LastSyncSucceeded = d.LastSyncSucceeded, LastSyncMessage = d.LastSyncMessage
            })
            .ToListAsync();

    public Task<SaveDeviceDto?> GetForEditAsync(int id)
        => _devices.QueryNoTracking().Where(d => d.Id == id)
            .Select(d => new SaveDeviceDto
            {
                Id = d.Id, Name = d.Name, IpAddress = d.IpAddress, Port = d.Port, CommKey = d.CommKey,
                IsEnabled = d.IsEnabled, PulledByServer = d.PulledByServer
            })
            .FirstOrDefaultAsync();

    public async Task<ServiceResult> CreateAsync(SaveDeviceDto dto)
    {
        var validation = await ValidateAsync(dto);
        if (!validation.Succeeded)
            return validation;

        var device = new AttendanceDevice { ApiKey = NewKey() };
        Map(dto, device);
        await _devices.AddAsync(device);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> UpdateAsync(SaveDeviceDto dto)
    {
        var device = await _devices.GetByIdAsync(dto.Id);
        if (device is null)
            return ServiceResult.Failure("الجهاز مش موجود");
        var validation = await ValidateAsync(dto);
        if (!validation.Succeeded)
            return validation;

        Map(dto, device);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var device = await _devices.GetByIdAsync(id);
        if (device is null)
            return ServiceResult.Failure("الجهاز مش موجود");
        _devices.Remove(device);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> NewApiKeyAsync(int id)
    {
        var device = await _devices.GetByIdAsync(id);
        if (device is null)
            return ServiceResult.Failure("الجهاز مش موجود");
        device.ApiKey = NewKey();
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public Task<List<int>> PulledDeviceIdsAsync()
        => _devices.QueryNoTracking().Where(d => d.IsEnabled && d.PulledByServer).Select(d => d.Id).ToListAsync();

    // ---------- Reading punches ----------

    public async Task<ServiceResult<PunchSyncResult>> PullAsync(int deviceId, CancellationToken cancellationToken = default)
    {
        var device = await _devices.GetByIdAsync(deviceId);
        if (device is null)
            return ServiceResult<PunchSyncResult>.Failure("الجهاز مش موجود");

        List<DevicePunch> punches;
        try
        {
            using var client = new ZkClient();
            await client.ConnectAsync(device.IpAddress, device.Port, device.CommKey, ConnectTimeout, cancellationToken);
            punches = await client.ReadAttendanceAsync(cancellationToken);
            await client.DisconnectAsync();
        }
        catch (Exception ex) when (ex is ZkException or System.Net.Sockets.SocketException or IOException or TimeoutException)
        {
            var message = ex is ZkException ? ex.Message : $"مش قادر يكلم الجهاز: {ex.Message}";
            _logger.LogWarning(ex, "Device {Device} pull failed", device.Name);
            await RecordAsync(device, false, message);
            return ServiceResult<PunchSyncResult>.Failure(message);
        }

        return await StoreAsync(device, punches);
    }

    public async Task<ServiceResult<PunchSyncResult>> ReceiveAsync(string apiKey, IReadOnlyList<DevicePunch> punches)
    {
        var device = await _devices.Query().FirstOrDefaultAsync(d => d.ApiKey == apiKey);
        if (device is null || !device.IsEnabled)
            return ServiceResult<PunchSyncResult>.Failure("مفتاح الجهاز غلط أو الجهاز موقوف");
        return await StoreAsync(device, punches);
    }

    private async Task<ServiceResult<PunchSyncResult>> StoreAsync(AttendanceDevice device, IReadOnlyList<DevicePunch> punches)
    {
        var now = DateTime.Now;

        // Keep each punch once (the device sends its whole log every time); ignore the future
        var incoming = punches
            .Where(p => !string.IsNullOrWhiteSpace(p.UserId) && p.Time <= now.AddMinutes(5))
            .Select(p => (Id: p.UserId.Trim(), Time: new DateTime(p.Time.Year, p.Time.Month, p.Time.Day, p.Time.Hour, p.Time.Minute, p.Time.Second)))
            .Distinct()
            .ToList();

        // Punches are raw log lines (no soft delete, no audit), so they live outside the repositories
        var punchSet = _context.AttendancePunches;
        var newPunches = new List<(string Id, DateTime Time)>();
        if (incoming.Count > 0)
        {
            var from = incoming.Min(p => p.Time);
            var known = (await punchSet.AsNoTracking()
                    .Where(p => p.PunchTime >= from)
                    .Select(p => new { p.FingerprintId, p.PunchTime })
                    .ToListAsync())
                .Select(p => (p.FingerprintId, p.PunchTime))
                .ToHashSet();
            newPunches = incoming.Where(p => !known.Contains((p.Id, p.Time))).ToList();
            foreach (var p in newPunches)
                await punchSet.AddAsync(new AttendancePunch { DeviceId = device.Id, FingerprintId = p.Id, PunchTime = p.Time, ReceivedAt = now });
        }

        // Rebuild the days that got new punches: first punch = check-in, last = check-out
        var employees = await _unitOfWork.Repository<Employee>().QueryNoTracking()
            .Where(e => e.FingerprintId != null)
            .ToDictionaryAsync(e => e.FingerprintId!, e => e.Code);
        var touched = newPunches.Select(p => (p.Id, Date: DateOnly.FromDateTime(p.Time))).Distinct().ToList();
        var unmatched = touched.Where(t => !employees.ContainsKey(t.Id)).Select(t => t.Id).Distinct().ToList();

        var rows = new List<AttendanceImportRow>();
        if (touched.Count > 0)
        {
            // Earlier punches of the same days (sent before) count too
            var minDay = touched.Min(t => t.Date).ToDateTime(TimeOnly.MinValue);
            var stored = (await punchSet.AsNoTracking().Where(p => p.PunchTime >= minDay)
                    .Select(p => new { p.FingerprintId, p.PunchTime }).ToListAsync())
                .Select(p => (Id: p.FingerprintId, p.PunchTime))
                .Concat(newPunches.Select(p => (p.Id, PunchTime: p.Time)))
                .ToList();

            var number = 0;
            foreach (var (id, date) in touched.Where(t => employees.ContainsKey(t.Id)))
            {
                var times = stored.Where(p => p.Id == id && DateOnly.FromDateTime(p.PunchTime) == date)
                    .Select(p => p.PunchTime).Distinct().OrderBy(t => t).ToList();
                var checkIn = times.First();
                var checkOut = times.Count > 1 ? times.Last() : (DateTime?)null;
                // Two punches a minute apart are one punch pressed twice
                if (checkOut is DateTime o && (o - checkIn).TotalMinutes < 2)
                    checkOut = null;
                rows.Add(new AttendanceImportRow(++number, employees[id], date.ToString("yyyy-MM-dd"),
                    checkIn.ToString("HH:mm"), checkOut?.ToString("HH:mm")));
            }
        }

        await _unitOfWork.SaveChangesAsync();

        var problems = new List<string>();
        var updated = 0;
        if (rows.Count > 0)
        {
            var results = await _attendance.ImportAsync(rows, apply: true);
            updated = results.Count(r => r.IsValid);
            problems.AddRange(results.Where(r => !r.IsValid).Select(r => $"{r.EmployeeName ?? r.Code} {r.Date:yyyy-MM-dd}: {r.Message}").Take(10));
        }
        if (unmatched.Count > 0)
            problems.Add($"أرقام بصمة مش متربطة بموظف: {string.Join("، ", unmatched.Take(10))}");

        var result = new PunchSyncResult(incoming.Count, newPunches.Count, unmatched.Count, updated, problems);
        var message = $"اتقرا {result.Received} بصمة، الجديد {result.New}، اتحدّث {result.DaysUpdated} يوم"
                      + (unmatched.Count > 0 ? $"، {unmatched.Count} رقم مش متربط" : "");
        await RecordAsync(device, true, message);
        return ServiceResult<PunchSyncResult>.Success(result);
    }

    private async Task RecordAsync(AttendanceDevice device, bool succeeded, string message)
    {
        device.LastSyncAt = DateTime.UtcNow;
        device.LastSyncSucceeded = succeeded;
        device.LastSyncMessage = message.Length > 500 ? message[..500] : message;
        await _unitOfWork.SaveChangesAsync();
    }

    // ---------- Helpers ----------

    private async Task<ServiceResult> ValidateAsync(SaveDeviceDto dto)
    {
        var name = dto.Name.Trim();
        if (await _devices.QueryNoTracking().AnyAsync(d => d.Name == name && d.Id != dto.Id))
            return ServiceResult.Failure("فيه جهاز بنفس الاسم", nameof(dto.Name));
        var host = dto.IpAddress.Trim();
        if (!IPAddress.TryParse(host, out _) && Uri.CheckHostName(host) == UriHostNameType.Unknown)
            return ServiceResult.Failure("عنوان IP مش صحيح (زي 192.168.1.201)", nameof(dto.IpAddress));
        return ServiceResult.Success();
    }

    private static void Map(SaveDeviceDto dto, AttendanceDevice device)
    {
        device.Name = dto.Name.Trim();
        device.IpAddress = dto.IpAddress.Trim();
        device.Port = dto.Port;
        device.CommKey = dto.CommKey;
        device.IsEnabled = dto.IsEnabled;
        device.PulledByServer = dto.PulledByServer;
    }

    private static string NewKey() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
}
