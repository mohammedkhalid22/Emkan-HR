using System.Globalization;
using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.DAL.Repositories;
using HRSystem.Domain.Entities;
using HRSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.BLL.Services;

public interface IAttendanceService
{
    Task<TodayAttendanceDto> GetTodayAsync(int employeeId);
    // location = where the browser says the employee is; locationError = why it couldn't tell;
    // selfie = the photo taken at that moment (required when the RequireSelfie setting is 1)
    Task<ServiceResult> CheckInAsync(int employeeId, GeoPoint? location = null, string? locationError = null, FileUpload? selfie = null);
    Task<ServiceResult> CheckOutAsync(int employeeId, GeoPoint? location = null, string? locationError = null, FileUpload? selfie = null);

    // The stored selfie of one check-in (or check-out), if the viewer may see that record
    Task<string?> GetSelfiePathAsync(int recordId, bool checkOut, EmployeeAccessScope scope);

    Task<PagedResult<AttendanceRecordDto>> GetRecordsAsync(AttendanceFilterDto filter, EmployeeAccessScope scope);
    Task<ManualAttendanceDto?> GetForEditAsync(int employeeId, DateOnly date);
    Task<ServiceResult> SaveManualAsync(ManualAttendanceDto dto);

    Task<int> CloseDaysAsync(DateOnly from, DateOnly to);
    Task<List<MonthlyAttendanceRowDto>> GetMonthlyReportAsync(int year, int month, int? departmentId);
    Task<List<AttendanceImportResultDto>> ImportAsync(IReadOnlyList<AttendanceImportRow> rows, bool apply);

    // Called when a permission is approved or cancelled for a day that already has a check-in
    Task RecalculateAsync(int employeeId, DateOnly date);
}

public class AttendanceService : IAttendanceService
{
    private const string OvertimeMinimumKey = "OvertimeMinimumMinutes";
    public const string RequireSelfieKey = "RequireSelfie";
    private const string SelfieFolder = "selfies";

    // Used only if no shift exists at all
    private static readonly ShiftTimes FallbackShift = new(new TimeOnly(9, 0), new TimeOnly(17, 0), 15);

    private static readonly string[] DateFormats = ["yyyy-MM-dd", "yyyy/MM/dd", "d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "dd-MM-yyyy"];
    private static readonly string[] TimeFormats = ["H:mm", "HH:mm", "H:mm:ss", "HH:mm:ss", "h:mm tt", "hh:mm tt"];

    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericRepository<AttendanceRecord> _records;
    private readonly IWorkCalendarService _calendar;
    private readonly ISettingsService _settings;
    private readonly IFileStorage _files;

    public AttendanceService(IUnitOfWork unitOfWork, IWorkCalendarService calendar, ISettingsService settings, IFileStorage files)
    {
        _files = files;
        _unitOfWork = unitOfWork;
        _records = unitOfWork.Repository<AttendanceRecord>();
        _calendar = calendar;
        _settings = settings;
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    // Seconds are dropped so minutes add up the way people read them
    private static TimeOnly Now
    {
        get
        {
            var now = DateTime.Now;
            return new TimeOnly(now.Hour, now.Minute);
        }
    }

    // ---------- Check in / out ----------

    public async Task<TodayAttendanceDto> GetTodayAsync(int employeeId)
    {
        var shiftId = await _unitOfWork.Repository<Employee>().QueryNoTracking()
            .Where(e => e.Id == employeeId).Select(e => e.ShiftId).FirstOrDefaultAsync();
        var shift = await GetShiftEntityAsync(shiftId);
        var record = await _records.QueryNoTracking().FirstOrDefaultAsync(r => r.EmployeeId == employeeId && r.Date == Today);

        return new TodayAttendanceDto
        {
            Date = Today,
            ShiftName = shift?.Name,
            ShiftStart = shift?.StartTime,
            ShiftEnd = shift?.EndTime,
            CheckIn = record?.CheckIn,
            CheckOut = record?.CheckOut,
            LateMinutes = record?.LateMinutes ?? 0,
            IsWorkingDay = await _calendar.IsWorkingDayAsync(Today),
            RequireSelfie = await _settings.GetIntAsync(RequireSelfieKey, 0) == 1
        };
    }

    public async Task<ServiceResult> CheckInAsync(int employeeId, GeoPoint? location = null, string? locationError = null, FileUpload? selfie = null)
    {
        var employee = await _unitOfWork.Repository<Employee>().QueryNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId);
        if (employee is null || employee.Status != EmployeeStatus.Active)
            return ServiceResult.Failure("لازم تكون موظف نشط عشان تسجل حضور");

        var record = await _records.Query().FirstOrDefaultAsync(r => r.EmployeeId == employeeId && r.Date == Today);
        if (record?.CheckIn is not null)
            return ServiceResult.Failure($"إنت مسجل حضور النهارده الساعة {record.CheckIn:HH:mm}");

        var place = await CheckLocationAsync(employeeId, location, locationError, "حضور");
        if (!place.Result.Succeeded)
            return place.Result;
        var photo = await CheckSelfieAsync(selfie, "حضور");
        if (!photo.Succeeded)
            return photo;

        if (record is null)
        {
            record = new AttendanceRecord { EmployeeId = employeeId, Date = Today };
            await _records.AddAsync(record);
        }

        record.CheckIn = Now;
        record.Status = AttendanceStatus.Present;
        record.Source = AttendanceSource.Web;
        record.CheckInLatitude = location?.Latitude;
        record.CheckInLongitude = location?.Longitude;
        record.CheckInDistanceMeters = place.DistanceMeters;
        record.CheckInPhotoPath = await SaveSelfieAsync(selfie);
        await ComputeAsync(record, employee.ShiftId);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> CheckOutAsync(int employeeId, GeoPoint? location = null, string? locationError = null, FileUpload? selfie = null)
    {
        var record = await _records.Query().Include(r => r.Employee)
            .FirstOrDefaultAsync(r => r.EmployeeId == employeeId && r.Date == Today);
        if (record?.CheckIn is null)
            return ServiceResult.Failure("لازم تسجل حضور الأول");
        if (record.CheckOut is not null)
            return ServiceResult.Failure($"إنت مسجل انصراف النهارده الساعة {record.CheckOut:HH:mm}");

        var now = Now;
        if (now <= record.CheckIn)
            return ServiceResult.Failure("وقت الانصراف لازم يكون بعد وقت الحضور");

        var place = await CheckLocationAsync(employeeId, location, locationError, "انصراف");
        if (!place.Result.Succeeded)
            return place.Result;
        var photo = await CheckSelfieAsync(selfie, "انصراف");
        if (!photo.Succeeded)
            return photo;

        record.CheckOut = now;
        record.CheckOutLatitude = location?.Latitude;
        record.CheckOutLongitude = location?.Longitude;
        record.CheckOutDistanceMeters = place.DistanceMeters;
        record.CheckOutPhotoPath = await SaveSelfieAsync(selfie);
        await ComputeAsync(record, record.Employee.ShiftId);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    private async Task<ServiceResult> CheckSelfieAsync(FileUpload? selfie, string action)
    {
        if (selfie is null)
            return await _settings.GetIntAsync(RequireSelfieKey, 0) == 1
                ? ServiceResult.Failure($"لازم تاخد صورة سيلفي عشان تسجل {action}. اسمح للبرنامج يفتح الكاميرا وجرّب تاني")
                : ServiceResult.Success();
        return FileRules.Validate(selfie, FileRules.PhotoExtensions) is string error
            ? ServiceResult.Failure("صورة السيلفي: " + error)
            : ServiceResult.Success();
    }

    private async Task<string?> SaveSelfieAsync(FileUpload? selfie)
        => selfie is null ? null : await _files.SaveAsync(selfie, $"{SelfieFolder}/{Today:yyyy-MM}");

    public async Task<string?> GetSelfiePathAsync(int recordId, bool checkOut, EmployeeAccessScope scope)
    {
        var record = await _records.QueryNoTracking()
            .Where(r => r.Id == recordId)
            .Select(r => new { r.EmployeeId, r.Employee.ManagerId, r.CheckInPhotoPath, r.CheckOutPhotoPath })
            .FirstOrDefaultAsync();
        if (record is null)
            return null;

        // Same reach as the records list: HR all, a manager their team, an employee their own
        var own = scope.OwnEmployeeId ?? -1;
        var allowed = scope.SeeAll || record.EmployeeId == own || (scope.SeeTeam && record.ManagerId == own);
        return allowed ? (checkOut ? record.CheckOutPhotoPath : record.CheckInPhotoPath) : null;
    }

    // The branch (through the department) decides whether and where the employee may check in.
    // Returns how far the employee is from the branch, when both locations are known.
    private async Task<(ServiceResult Result, int? DistanceMeters)> CheckLocationAsync(
        int employeeId, GeoPoint? location, string? locationError, string action)
    {
        if (location is not null && !GeoCalculator.IsValid(location))
            return (ServiceResult.Failure("الموقع اللي وصل من الموبايل مش صحيح. جرّب تاني"), null);

        var info = await _unitOfWork.Repository<Employee>().QueryNoTracking()
            .Where(e => e.Id == employeeId)
            .Select(e => new
            {
                e.AllowRemoteAttendance,
                BranchName = e.Department.Branch!.Name,
                Latitude = e.Department.Branch!.Latitude,
                Longitude = e.Department.Branch!.Longitude,
                Radius = (int?)e.Department.Branch!.AttendanceRadiusMeters
            })
            .FirstAsync();

        if (info.Latitude is not double branchLat || info.Longitude is not double branchLng)
            return (ServiceResult.Success(), null);

        int? distance = location is null
            ? null
            : (int)Math.Round(GeoCalculator.DistanceMeters(location.Latitude, location.Longitude, branchLat, branchLng));

        if (info.AllowRemoteAttendance)
            return (ServiceResult.Success(), distance);

        if (location is null)
            return (ServiceResult.Failure(
                $"لازم نعرف مكانك عشان تسجل {action}. {locationError ?? "اسمح للبرنامج يعرف مكانك وجرّب تاني"}"), null);

        var radius = info.Radius ?? 200;
        return GeoCalculator.Check(location, branchLat, branchLng, radius) switch
        {
            LocationCheck.TooInaccurate => (ServiceResult.Failure(
                $"مكانك مش واضح (الدقة حوالي {location.AccuracyMeters:0} متر). شغّل الـ GPS أو اطلع جنب شباك وجرّب تاني"), distance),
            LocationCheck.OutsideBranch => (ServiceResult.Failure(
                $"إنت بعيد عن «{info.BranchName}» بحوالي {distance} متر، والمسموح {radius} متر. لازم تكون في الفرع عشان تسجل {action}"), distance),
            _ => (ServiceResult.Success(), distance)
        };
    }

    // ---------- Records ----------

    public async Task<PagedResult<AttendanceRecordDto>> GetRecordsAsync(AttendanceFilterDto filter, EmployeeAccessScope scope)
    {
        var from = filter.From ?? Today;
        var to = filter.To ?? from;
        var query = _records.QueryNoTracking().Where(r => r.Date >= from && r.Date <= to);

        if (!scope.SeeAll)
        {
            var own = scope.OwnEmployeeId ?? -1;
            query = scope.SeeTeam
                ? query.Where(r => r.EmployeeId == own || r.Employee.ManagerId == own)
                : query.Where(r => r.EmployeeId == own);
        }

        if (filter.DepartmentId is int departmentId)
            query = query.Where(r => r.Employee.DepartmentId == departmentId);
        if (filter.Status is AttendanceStatus status)
            query = query.Where(r => r.Status == status);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(r => r.Employee.FullNameAr.Contains(term) || r.Employee.Code.Contains(term));
        }

        var page = Math.Max(1, filter.Page);
        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.Date).ThenBy(r => r.Employee.Code)
            .Skip((page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(r => new AttendanceRecordDto
            {
                Id = r.Id,
                EmployeeId = r.EmployeeId,
                EmployeeCode = r.Employee.Code,
                EmployeeName = r.Employee.FullNameAr,
                DepartmentName = r.Employee.Department.Name,
                Date = r.Date,
                CheckIn = r.CheckIn,
                CheckOut = r.CheckOut,
                LateMinutes = r.LateMinutes,
                EarlyLeaveMinutes = r.EarlyLeaveMinutes,
                OvertimeMinutes = r.OvertimeMinutes,
                Status = r.Status,
                Source = r.Source,
                Notes = r.Notes,
                CheckInLatitude = r.CheckInLatitude,
                CheckInLongitude = r.CheckInLongitude,
                CheckInDistanceMeters = r.CheckInDistanceMeters,
                CheckOutLatitude = r.CheckOutLatitude,
                CheckOutLongitude = r.CheckOutLongitude,
                CheckOutDistanceMeters = r.CheckOutDistanceMeters,
                HasCheckInPhoto = r.CheckInPhotoPath != null,
                HasCheckOutPhoto = r.CheckOutPhotoPath != null,
                BranchRadiusMeters = (int?)r.Employee.Department.Branch!.AttendanceRadiusMeters
            })
            .ToListAsync();

        return new PagedResult<AttendanceRecordDto> { Items = items, TotalCount = total, Page = page, PageSize = filter.PageSize };
    }

    public async Task<ManualAttendanceDto?> GetForEditAsync(int employeeId, DateOnly date)
    {
        var name = await _unitOfWork.Repository<Employee>().QueryNoTracking()
            .Where(e => e.Id == employeeId).Select(e => e.FullNameAr).FirstOrDefaultAsync();
        if (name is null)
            return null;

        var record = await _records.QueryNoTracking().FirstOrDefaultAsync(r => r.EmployeeId == employeeId && r.Date == date);
        return new ManualAttendanceDto
        {
            EmployeeId = employeeId,
            EmployeeName = name,
            Date = date,
            CheckIn = record?.CheckIn,
            CheckOut = record?.CheckOut,
            Status = record?.Status ?? AttendanceStatus.Present,
            Notes = record?.Notes ?? ""
        };
    }

    public async Task<ServiceResult> SaveManualAsync(ManualAttendanceDto dto)
    {
        var employee = await _unitOfWork.Repository<Employee>().QueryNoTracking().FirstOrDefaultAsync(e => e.Id == dto.EmployeeId);
        if (employee is null)
            return ServiceResult.Failure("الموظف مش موجود");

        var date = dto.Date!.Value;
        if (date > Today)
            return ServiceResult.Failure("مينفعش تسجل حضور ليوم لسه مجاش", nameof(dto.Date));
        if (date < employee.HireDate)
            return ServiceResult.Failure("التاريخ قبل تاريخ تعيين الموظف", nameof(dto.Date));

        var isPresent = dto.Status == AttendanceStatus.Present;
        if (isPresent && dto.CheckIn is null)
            return ServiceResult.Failure("الموظف الحاضر لازم يبقى ليه وقت حضور", nameof(dto.CheckIn));
        if (dto.CheckIn is TimeOnly i && dto.CheckOut is TimeOnly o && o <= i)
            return ServiceResult.Failure("وقت الانصراف لازم يكون بعد وقت الحضور", nameof(dto.CheckOut));

        var record = await _records.Query().FirstOrDefaultAsync(r => r.EmployeeId == dto.EmployeeId && r.Date == date);
        if (record is null)
        {
            record = new AttendanceRecord { EmployeeId = dto.EmployeeId, Date = date };
            await _records.AddAsync(record);
        }

        record.Status = dto.Status!.Value;
        record.CheckIn = isPresent ? dto.CheckIn : null;
        record.CheckOut = isPresent ? dto.CheckOut : null;
        record.Source = AttendanceSource.Manual;
        record.Notes = dto.Notes.Trim();
        await ComputeAsync(record, employee.ShiftId);

        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task RecalculateAsync(int employeeId, DateOnly date)
    {
        var record = await _records.Query().Include(r => r.Employee)
            .FirstOrDefaultAsync(r => r.EmployeeId == employeeId && r.Date == date);
        if (record?.CheckIn is null)
            return;

        await ComputeAsync(record, record.Employee.ShiftId);
        await _unitOfWork.SaveChangesAsync();
    }

    // ---------- Daily close ----------

    // For each past day: employees without a check-in get Absent, OnLeave, Mission or Holiday.
    // Safe to run again: it only creates missing rows and refreshes the ones it created itself
    // (e.g. a leave approved after the day was marked absent).
    public async Task<int> CloseDaysAsync(DateOnly from, DateOnly to)
    {
        if (to >= Today)
            to = Today.AddDays(-1);
        if (from > to)
            return 0;

        var weekend = await _calendar.GetWeekendAsync();
        var holidays = await _calendar.GetHolidaysAsync(from, to);

        var employees = await _unitOfWork.Repository<Employee>().QueryNoTracking()
            .Where(e => e.HireDate <= to && (e.TerminationDate == null || e.TerminationDate >= from))
            .Select(e => new { e.Id, e.HireDate, e.TerminationDate })
            .ToListAsync();

        var leaves = await _unitOfWork.Repository<LeaveRequest>().QueryNoTracking()
            .Where(l => l.Status == RequestStatus.Approved && l.StartDate <= to && l.EndDate >= from)
            .Select(l => new { l.EmployeeId, l.StartDate, l.EndDate })
            .ToListAsync();

        var missions = await _unitOfWork.Repository<AttendanceRequest>().QueryNoTracking()
            .Where(m => m.Type == AttendanceRequestType.Mission && m.Status == RequestStatus.Approved && m.Date >= from && m.Date <= to)
            .Select(m => new { m.EmployeeId, m.Date })
            .ToListAsync();

        var existing = await _records.Query()
            .Where(r => r.Date >= from && r.Date <= to)
            .ToDictionaryAsync(r => (r.EmployeeId, r.Date));

        var changed = 0;
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var isHoliday = holidays.Contains(day);
            var isWeekend = weekend.Contains(day.DayOfWeek);

            foreach (var e in employees.Where(e => e.HireDate <= day && (e.TerminationDate == null || e.TerminationDate >= day)))
            {
                existing.TryGetValue((e.Id, day), out var record);

                // Real attendance and HR edits are never touched
                if (record is not null && (record.CheckIn is not null || record.Source != AttendanceSource.System))
                    continue;

                AttendanceStatus? status =
                    leaves.Any(l => l.EmployeeId == e.Id && l.StartDate <= day && l.EndDate >= day) ? AttendanceStatus.OnLeave
                    : missions.Any(m => m.EmployeeId == e.Id && m.Date == day) ? AttendanceStatus.Mission
                    : isHoliday ? AttendanceStatus.Holiday
                    : isWeekend ? null
                    : AttendanceStatus.Absent;

                if (status is null)
                {
                    // Weekend: no row needed; drop one the close made earlier if the settings changed
                    if (record is not null)
                    {
                        _records.Remove(record);
                        changed++;
                    }
                    continue;
                }

                if (record is null)
                {
                    await _records.AddAsync(new AttendanceRecord
                    {
                        EmployeeId = e.Id,
                        Date = day,
                        Status = status.Value,
                        Source = AttendanceSource.System
                    });
                    changed++;
                }
                else if (record.Status != status)
                {
                    record.Status = status.Value;
                    changed++;
                }
            }
        }

        await _unitOfWork.SaveChangesAsync();
        return changed;
    }

    // ---------- Monthly report ----------

    public async Task<List<MonthlyAttendanceRowDto>> GetMonthlyReportAsync(int year, int month, int? departmentId)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        var employees = _unitOfWork.Repository<Employee>().QueryNoTracking()
            .Where(e => e.HireDate <= to && (e.TerminationDate == null || e.TerminationDate >= from));
        if (departmentId is int id)
            employees = employees.Where(e => e.DepartmentId == id);

        var rows = await employees
            .OrderBy(e => e.Code)
            .Select(e => new
            {
                e.Id,
                e.Code,
                e.FullNameAr,
                Department = e.Department.Name,
                Records = e.AttendanceRecords.Where(r => r.Date >= from && r.Date <= to)
                    .Select(r => new { r.Status, r.LateMinutes, r.EarlyLeaveMinutes, r.OvertimeMinutes })
                    .ToList()
            })
            .ToListAsync();

        return rows.Select(e => new MonthlyAttendanceRowDto
        {
            EmployeeId = e.Id,
            EmployeeCode = e.Code,
            EmployeeName = e.FullNameAr,
            DepartmentName = e.Department,
            PresentDays = e.Records.Count(r => r.Status == AttendanceStatus.Present),
            AbsentDays = e.Records.Count(r => r.Status == AttendanceStatus.Absent),
            LeaveDays = e.Records.Count(r => r.Status == AttendanceStatus.OnLeave),
            MissionDays = e.Records.Count(r => r.Status == AttendanceStatus.Mission),
            HolidayDays = e.Records.Count(r => r.Status == AttendanceStatus.Holiday),
            LateMinutes = e.Records.Sum(r => r.LateMinutes),
            EarlyLeaveMinutes = e.Records.Sum(r => r.EarlyLeaveMinutes),
            OvertimeMinutes = e.Records.Sum(r => r.OvertimeMinutes)
        }).ToList();
    }

    // ---------- Fingerprint import ----------

    public async Task<List<AttendanceImportResultDto>> ImportAsync(IReadOnlyList<AttendanceImportRow> rows, bool apply)
    {
        var employees = await _unitOfWork.Repository<Employee>().QueryNoTracking()
            .Select(e => new { e.Id, e.Code, e.FullNameAr, e.Status, e.HireDate, e.ShiftId })
            .ToDictionaryAsync(e => e.Code, StringComparer.OrdinalIgnoreCase);

        var results = new List<AttendanceImportResultDto>();
        var seen = new HashSet<(string, DateOnly)>();

        foreach (var row in rows)
        {
            var result = new AttendanceImportResultDto { RowNumber = row.RowNumber, Code = row.Code?.Trim() };
            results.Add(result);

            if (string.IsNullOrWhiteSpace(result.Code) || !employees.TryGetValue(result.Code, out var employee))
            {
                result.Message = "كود الموظف مش موجود";
                continue;
            }
            result.EmployeeName = employee.FullNameAr;

            if (!DateOnly.TryParseExact(row.Date?.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                result.Message = "التاريخ مش مفهوم (المطلوب yyyy-MM-dd)";
                continue;
            }
            result.Date = date;

            if (!TryParseTime(row.CheckIn, out var checkIn) || checkIn is null)
            {
                result.Message = "وقت الحضور مش مفهوم (المطلوب HH:mm)";
                continue;
            }
            if (!TryParseTime(row.CheckOut, out var checkOut))
            {
                result.Message = "وقت الانصراف مش مفهوم (المطلوب HH:mm)";
                continue;
            }
            result.CheckIn = checkIn;
            result.CheckOut = checkOut;

            if (date > Today)
                result.Message = "التاريخ لسه مجاش";
            else if (date < employee.HireDate)
                result.Message = "التاريخ قبل تاريخ التعيين";
            else if (checkOut is TimeOnly o && o <= checkIn)
                result.Message = "الانصراف قبل الحضور";
            else if (!seen.Add((employee.Code, date)))
                result.Message = "الموظف ده متكرر في نفس اليوم في الملف";
            else
                result.IsValid = true;
        }

        // Rows HR already edited by hand are kept as they are
        var validKeys = results.Where(r => r.IsValid).Select(r => (employees[r.Code!].Id, r.Date!.Value)).ToList();
        var dates = validKeys.Select(k => k.Item2).Distinct().ToList();
        var existing = await _records.Query().Where(r => dates.Contains(r.Date)).ToListAsync();

        foreach (var result in results.Where(r => r.IsValid))
        {
            var employee = employees[result.Code!];
            var record = existing.FirstOrDefault(r => r.EmployeeId == employee.Id && r.Date == result.Date);

            if (record?.Source == AttendanceSource.Manual)
            {
                result.IsValid = false;
                result.Message = "اليوم ده اتعدل يدويًا، مش هيتغير";
                continue;
            }
            result.Message = record is null ? "جديد" : "هيحدّث تسجيل موجود";

            if (!apply)
                continue;

            if (record is null)
            {
                record = new AttendanceRecord { EmployeeId = employee.Id, Date = result.Date!.Value };
                await _records.AddAsync(record);
            }

            record.CheckIn = result.CheckIn;
            record.CheckOut = result.CheckOut;
            record.Status = AttendanceStatus.Present;
            record.Source = AttendanceSource.Device;
            await ComputeAsync(record, employee.ShiftId);
        }

        if (apply)
            await _unitOfWork.SaveChangesAsync();

        return results;
    }

    private static bool TryParseTime(string? text, out TimeOnly? time)
    {
        time = null;
        if (string.IsNullOrWhiteSpace(text))
            return true; // empty is allowed (e.g. no check-out)

        if (TimeOnly.TryParseExact(text.Trim(), TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            time = new TimeOnly(parsed.Hour, parsed.Minute);
            return true;
        }
        return false;
    }

    // ---------- Calculation ----------

    // Fills late / early leave / overtime minutes from the shift, the calendar and approved permissions
    private async Task ComputeAsync(AttendanceRecord record, int? shiftId)
    {
        if (record.CheckIn is null)
        {
            record.LateMinutes = record.EarlyLeaveMinutes = record.OvertimeMinutes = 0;
            return;
        }

        var shiftEntity = await GetShiftEntityAsync(shiftId);
        var shift = shiftEntity is null ? FallbackShift : new ShiftTimes(shiftEntity.StartTime, shiftEntity.EndTime, shiftEntity.GraceMinutes);

        var permissions = await _unitOfWork.Repository<AttendanceRequest>().QueryNoTracking()
            .Where(p => p.EmployeeId == record.EmployeeId && p.Date == record.Date && p.Status == RequestStatus.Approved)
            .Select(p => new TimeRange(p.FromTime, p.ToTime))
            .ToListAsync();

        var result = AttendanceCalculator.Calculate(
            shift, record.CheckIn, record.CheckOut,
            await _calendar.IsWorkingDayAsync(record.Date),
            permissions,
            await _settings.GetIntAsync(OvertimeMinimumKey, 30));

        record.LateMinutes = result.LateMinutes;
        record.EarlyLeaveMinutes = result.EarlyLeaveMinutes;
        record.OvertimeMinutes = result.OvertimeMinutes;
    }

    // The employee's shift, or the first shift as the company default
    private async Task<Shift?> GetShiftEntityAsync(int? shiftId)
    {
        var shifts = _unitOfWork.Repository<Shift>().QueryNoTracking();
        return (shiftId is int id ? await shifts.FirstOrDefaultAsync(s => s.Id == id) : null)
            ?? await shifts.OrderBy(s => s.Id).FirstOrDefaultAsync();
    }
}
