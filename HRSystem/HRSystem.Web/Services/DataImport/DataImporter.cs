using System.ComponentModel.DataAnnotations;
using System.Globalization;
using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.BLL.Services;
using HRSystem.DAL.Data;
using HRSystem.Domain.Entities;
using HRSystem.Domain.Enums;
using HRSystem.Web.Helpers;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.Web.Services.DataImport;

public enum ImportOutcome { Created, Updated, Skipped, Error }

public record ImportRowResult(string Sheet, int RowNumber, string Label, ImportOutcome Outcome, string Message);

public class ImportReport
{
    public List<ImportRowResult> Rows { get; } = [];
    public List<string> Warnings { get; } = [];
    public bool Applied { get; set; }
    public int? BatchId { get; set; }

    public int Count(ImportOutcome outcome) => Rows.Count(r => r.Outcome == outcome);
    public IEnumerable<ImportRowResult> Of(string sheet) => Rows.Where(r => r.Sheet == sheet);
}

// Saves the basic-data workbook through the same services the screens use, sheet by sheet in
// dependency order, so every row passes the screens' own checks. A preview runs everything in a
// database transaction and rolls it back: what it shows is exactly what saving would do.
public class DataImporter
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DataImporter> _logger;

    public DataImporter(IServiceScopeFactory scopeFactory, ILogger<DataImporter> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // selected = "sheet:row" keys to save; null = every row (the preview)
    public async Task<ImportReport> RunAsync(ImportFile file, string fileName, ISet<string>? selected, bool apply)
    {
        using var scope = _scopeFactory.CreateScope();
        var run = new Run(scope.ServiceProvider);
        var report = new ImportReport();
        report.Warnings.AddRange(file.Warnings);

        await using var transaction = await run.Db.Database.BeginTransactionAsync();
        foreach (var sheet in ImportSheets.All)
        {
            if (!file.Sheets.TryGetValue(sheet.Key, out var rows))
                continue;
            var chosen = new List<ImportRow>();
            foreach (var row in rows)
            {
                if (selected is null || selected.Contains($"{sheet.Key}:{row.RowNumber}"))
                    chosen.Add(row);
                else
                    report.Rows.Add(new ImportRowResult(sheet.Key, row.RowNumber, run.Label(sheet, row), ImportOutcome.Skipped, "مش متختار"));
            }
            await run.SheetAsync(sheet, chosen, report);
        }

        if (!apply)
        {
            await transaction.RollbackAsync();
            return report;
        }

        var created = report.Count(ImportOutcome.Created);
        var updated = report.Count(ImportOutcome.Updated);
        var batch = new ImportBatch
        {
            FileName = Path.GetFileName(fileName) is { Length: > 0 } n ? (n.Length > 200 ? n[..200] : n) : "import.xlsx",
            Summary = $"{created} جديد، {updated} اتحدّث، {report.Count(ImportOutcome.Error)} مرفوض"
        };
        foreach (var item in run.Items)
            batch.Items.Add(item);
        run.Db.ImportBatches.Add(batch);
        await run.Db.SaveChangesAsync();
        await transaction.CommitAsync();

        report.Applied = true;
        report.BatchId = batch.Id;
        return report;
    }

    public record BatchDto(int Id, string FileName, string Summary, DateTime CreatedAt, string? By, DateTime? UndoneAt, int CreatedCount);

    public async Task<List<BatchDto>> HistoryAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HRDbContext>();
        var batches = await db.ImportBatches.AsNoTracking().OrderByDescending(b => b.Id).Take(20)
            .Select(b => new { b.Id, b.FileName, b.Summary, b.CreatedAt, b.CreatedBy, b.UndoneAt, Created = b.Items.Count(i => i.Created) })
            .ToListAsync();
        var userIds = batches.Select(b => b.CreatedBy).Where(u => u != null).Distinct().ToList();
        var names = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
        return batches.Select(b => new BatchDto(b.Id, b.FileName, b.Summary, b.CreatedAt, b.CreatedBy is null ? null : names.GetValueOrDefault(b.CreatedBy), b.UndoneAt, b.Created)).ToList();
    }

    // Names already in the system, for the template's dropdowns
    public async Task<Dictionary<string, List<string>>> ReferencesAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HRDbContext>();
        return new()
        {
            ["branches"] = await db.Branches.OrderBy(x => x.Name).Select(x => x.Name).ToListAsync(),
            ["departments"] = await db.Departments.OrderBy(x => x.Name).Select(x => x.Name).ToListAsync(),
            ["jobtitles"] = await db.JobTitles.OrderBy(x => x.Name).Select(x => x.Name).ToListAsync(),
            ["positions"] = await db.Positions.OrderBy(x => x.Name).Select(x => x.Name).Distinct().ToListAsync(),
            ["shifts"] = await db.Shifts.OrderBy(x => x.Name).Select(x => x.Name).ToListAsync()
        };
    }

    // Removes what an import created, newest first, through the screens' own delete rules
    public async Task<(int Removed, List<string> Kept)> UndoAsync(int batchId)
    {
        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<HRDbContext>();
        var batch = await db.ImportBatches.Include(b => b.Items).FirstOrDefaultAsync(b => b.Id == batchId);
        if (batch is null || batch.UndoneAt is not null)
            return (0, ["الاستيراد ده مش موجود أو اتلغى قبل كده"]);

        var removed = 0;
        var kept = new List<string>();
        foreach (var item in batch.Items.Where(i => i.Created).OrderByDescending(i => i.Id))
        {
            ServiceResult result;
            // Each delete starts clean: an earlier one may have loaded the same rows
            db.ChangeTracker.Clear();
            try
            {
                result = item.EntityName switch
                {
                    nameof(Employee) => await RemoveEmployeeAsync(db, item.EntityId),
                    nameof(Position) => await sp.GetRequiredService<IPositionService>().DeleteAsync(item.EntityId),
                    nameof(Department) => await sp.GetRequiredService<IDepartmentService>().DeleteAsync(item.EntityId),
                    nameof(Branch) => await sp.GetRequiredService<IBranchService>().DeleteAsync(item.EntityId),
                    nameof(JobTitle) => await sp.GetRequiredService<IJobTitleService>().DeleteAsync(item.EntityId),
                    nameof(Shift) => await sp.GetRequiredService<IShiftService>().DeleteAsync(item.EntityId),
                    nameof(LeaveType) => await sp.GetRequiredService<ILeaveTypeService>().DeleteAsync(item.EntityId),
                    nameof(PublicHoliday) => await sp.GetRequiredService<IPublicHolidayService>().DeleteAsync(item.EntityId),
                    nameof(SalaryComponent) => await sp.GetRequiredService<ISalaryComponentService>().DeleteAsync(item.EntityId),
                    _ => ServiceResult.Failure("نوع مش معروف")
                };
            }
            catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Undo of {Entity} {Id} failed", item.EntityName, item.EntityId);
                db.ChangeTracker.Clear();
                result = ServiceResult.Failure("مقدرش يتمسح");
            }
            if (result.Succeeded)
                removed++;
            else
                kept.Add($"{Run.EntityLabel(item.EntityName)} #{item.EntityId}: {result.Error}");
        }

        var fresh = await db.ImportBatches.FirstAsync(b => b.Id == batchId);
        fresh.UndoneAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await OrgManagers.SyncAsync(sp.GetRequiredService<HRSystem.DAL.Repositories.IUnitOfWork>());
        return (removed, kept);
    }

    // An imported employee goes only while nothing has been recorded for them yet
    private static async Task<ServiceResult> RemoveEmployeeAsync(HRDbContext db, int id)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == id);
        if (employee is null)
            return ServiceResult.Success();
        if (employee.UserId is not null)
            return ServiceResult.Failure("اتعمله حساب دخول");
        if (await db.AttendanceRecords.AnyAsync(r => r.EmployeeId == id) || await db.LeaveRequests.AnyAsync(r => r.EmployeeId == id)
            || await db.PayrollItems.AnyAsync(p => p.EmployeeId == id))
            return ServiceResult.Failure("اتسجلّه حضور أو إجازات أو مرتبات");
        if (await db.Employees.AnyAsync(e => e.ManagerId == id && e.PositionId == null))
            return ServiceResult.Failure("مدير مباشر لموظفين تانيين");

        db.Employees.Remove(employee);
        await db.SaveChangesAsync();
        return ServiceResult.Success();
    }

    // ---------- One run (one scope, one transaction) ----------

    private sealed class Run(IServiceProvider sp)
    {
        public HRDbContext Db { get; } = sp.GetRequiredService<HRDbContext>();
        public List<ImportBatchItem> Items { get; } = [];

        private T Service<T>() where T : notnull => sp.GetRequiredService<T>();

        public static string EntityLabel(string entity) => entity switch
        {
            nameof(Employee) => "موظف", nameof(Position) => "موقع", nameof(Department) => "إدارة / قسم",
            nameof(Branch) => "فرع", nameof(JobTitle) => "مسمى", nameof(Shift) => "وردية",
            nameof(LeaveType) => "نوع إجازة", nameof(PublicHoliday) => "عطلة", nameof(SalaryComponent) => "مكوّن مرتب",
            _ => entity
        };

        public string Label(ImportSheet sheet, ImportRow row) => sheet.Key switch
        {
            "employees" => $"{row.Get("namear")} ({row.Get("nationalid")})",
            "holidays" => $"{row.Get("date")} {row.Get("name")}",
            "positions" => $"{row.Get("name")} — {row.Get("department")}",
            _ => row.Get("name") ?? "—"
        };

        public async Task SheetAsync(ImportSheet sheet, List<ImportRow> rows, ImportReport report)
        {
            // Rows that point at another row of the same sheet (a section under its administration,
            // a position under its parent, an employee under their manager) wait until that one is in
            var pending = rows.ToList();
            var postponed = new Dictionary<int, string>();
            while (pending.Count > 0)
            {
                var progress = false;
                foreach (var row in pending.ToList())
                {
                    var outcome = await RowAsync(sheet, row);
                    if (outcome is { Outcome: ImportOutcome.Skipped } waiting)
                    {
                        postponed[row.RowNumber] = waiting.Message;
                        continue;
                    }
                    report.Rows.Add(outcome with { Label = Label(sheet, row) });
                    pending.Remove(row);
                    progress = true;
                }
                if (progress)
                    continue;

                // Nothing moved: what is left points at something that is not there
                foreach (var row in pending)
                    report.Rows.Add(new ImportRowResult(sheet.Key, row.RowNumber, Label(sheet, row), ImportOutcome.Error, postponed[row.RowNumber]));
                break;
            }
        }

        private async Task<ImportRowResult> RowAsync(ImportSheet sheet, ImportRow row)
        {
            try
            {
                var result = sheet.Key switch
                {
                    "branches" => await BranchAsync(row),
                    "departments" => await DepartmentAsync(row),
                    "jobtitles" => await JobTitleAsync(row),
                    "shifts" => await ShiftAsync(row),
                    "positions" => await PositionAsync(row),
                    "leavetypes" => await LeaveTypeAsync(row),
                    "holidays" => await HolidayAsync(row),
                    "components" => await ComponentAsync(row),
                    "employees" => await EmployeeAsync(row),
                    _ => Fail("شيت مش معروف")
                };
                Db.ChangeTracker.Clear();
                return result with { Sheet = sheet.Key, RowNumber = row.RowNumber };
            }
            catch (ImportProblem problem)
            {
                Db.ChangeTracker.Clear();
                return new ImportRowResult(sheet.Key, row.RowNumber, "", problem.Wait ? ImportOutcome.Skipped : ImportOutcome.Error, problem.Message);
            }
            catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException or FormatException)
            {
                Db.ChangeTracker.Clear();
                return new ImportRowResult(sheet.Key, row.RowNumber, "", ImportOutcome.Error, "مقدرش يتحفظ: " + (ex.InnerException?.Message ?? ex.Message));
            }
        }

        // ---------- Sheets ----------

        private async Task<ImportRowResult> BranchAsync(ImportRow row)
        {
            var name = Need(row, "name", "اسم الفرع");
            var id = await Db.Branches.Where(b => b.Name == name).Select(b => (int?)b.Id).FirstOrDefaultAsync();
            var service = Service<IBranchService>();
            var dto = id is int existing ? (await service.GetForEditAsync(existing))! : new SaveBranchDto();
            dto.Name = name;
            dto.Address = row.Get("address") ?? dto.Address;
            if (row.Get("lat") is { } lat) dto.Latitude = (double)Dec(lat, "خط العرض");
            if (row.Get("lng") is { } lng) dto.Longitude = (double)Dec(lng, "خط الطول");
            if (row.Get("radius") is { } radius) dto.AttendanceRadiusMeters = Int(radius, "نطاق الحضور");
            return await SaveAsync(dto, id, () => service.CreateAsync(dto), () => service.UpdateAsync(dto), nameof(Branch),
                () => Db.Branches.Where(b => b.Name == name).Select(b => b.Id).FirstAsync());
        }

        private async Task<ImportRowResult> DepartmentAsync(ImportRow row)
        {
            var name = Need(row, "name", "الاسم");
            var id = await Db.Departments.Where(d => d.Name == name).Select(d => (int?)d.Id).FirstOrDefaultAsync();
            var service = Service<IDepartmentService>();
            var dto = id is int existing ? (await service.GetForEditAsync(existing))! : new SaveDepartmentDto { Type = null };
            dto.Name = name;
            if (row.Get("type") is { } type) dto.Type = Enum<DepartmentType>(type, "النوع", Labels.Of);
            if (dto.Type is null) throw new ImportProblem("النوع مطلوب (إدارة أو قسم)");
            if (row.Get("parent") is { } parent)
                dto.ParentDepartmentId = await Db.Departments.Where(d => d.Name == parent).Select(d => (int?)d.Id).FirstOrDefaultAsync()
                    ?? throw new ImportProblem($"«{parent}» مش موجودة", wait: true);
            if (row.Get("branch") is { } branch)
                dto.BranchId = await Db.Branches.Where(b => b.Name == branch).Select(b => (int?)b.Id).FirstOrDefaultAsync()
                    ?? throw new ImportProblem($"الفرع «{branch}» مش موجود");
            return await SaveAsync(dto, id, () => service.CreateAsync(dto), () => service.UpdateAsync(dto), nameof(Department),
                () => Db.Departments.Where(d => d.Name == name).Select(d => d.Id).FirstAsync());
        }

        private async Task<ImportRowResult> JobTitleAsync(ImportRow row)
        {
            var name = Need(row, "name", "المسمى الوظيفي");
            var id = await Db.JobTitles.Where(j => j.Name == name).Select(j => (int?)j.Id).FirstOrDefaultAsync();
            var service = Service<IJobTitleService>();
            var dto = id is int existing ? (await service.GetForEditAsync(existing))! : new SaveJobTitleDto();
            dto.Name = name;
            if (row.Get("min") is { } min) dto.MinSalary = Dec(min, "الحد الأدنى");
            if (row.Get("max") is { } max) dto.MaxSalary = Dec(max, "الحد الأقصى");
            dto.Requirements = row.Get("requirements") ?? dto.Requirements;
            if (dto.MinSalary > dto.MaxSalary) throw new ImportProblem("الحد الأدنى أكبر من الحد الأقصى");
            return await SaveAsync(dto, id, () => service.CreateAsync(dto), () => service.UpdateAsync(dto), nameof(JobTitle),
                () => Db.JobTitles.Where(j => j.Name == name).Select(j => j.Id).FirstAsync());
        }

        private async Task<ImportRowResult> ShiftAsync(ImportRow row)
        {
            var name = Need(row, "name", "اسم الوردية");
            var id = await Db.Shifts.Where(s => s.Name == name).Select(s => (int?)s.Id).FirstOrDefaultAsync();
            var service = Service<IShiftService>();
            var dto = id is int existing ? (await service.GetForEditAsync(existing))! : new SaveShiftDto();
            dto.Name = name;
            if (row.Get("start") is { } start) dto.StartTime = Time(start, "البداية");
            if (row.Get("end") is { } end) dto.EndTime = Time(end, "النهاية");
            if (row.Get("grace") is { } grace) dto.GraceMinutes = Int(grace, "دقايق السماح");
            return await SaveAsync(dto, id, () => service.CreateAsync(dto), () => service.UpdateAsync(dto), nameof(Shift),
                () => Db.Shifts.Where(s => s.Name == name).Select(s => s.Id).FirstAsync());
        }

        private async Task<ImportRowResult> PositionAsync(ImportRow row)
        {
            var name = Need(row, "name", "اسم الموقع");
            var departmentName = Need(row, "department", "الإدارة / القسم");
            var departmentId = await Db.Departments.Where(d => d.Name == departmentName).Select(d => (int?)d.Id).FirstOrDefaultAsync()
                ?? throw new ImportProblem($"الإدارة / القسم «{departmentName}» مش موجود");
            var id = await Db.Positions.Where(p => p.Name == name && p.DepartmentId == departmentId).Select(p => (int?)p.Id).FirstOrDefaultAsync();
            var service = Service<IPositionService>();
            var dto = id is int existing ? (await service.GetForEditAsync(existing))! : new SavePositionDto();
            dto.Name = name;
            dto.DepartmentId = departmentId;
            if (row.Get("jobtitle") is { } job)
                dto.JobTitleId = await Db.JobTitles.Where(j => j.Name == job).Select(j => (int?)j.Id).FirstOrDefaultAsync()
                    ?? throw new ImportProblem($"المسمى «{job}» مش موجود");
            if (row.Get("parent") is { } parent)
                dto.ParentPositionId = await FindPositionAsync(parent);
            if (row.Get("capacity") is { } capacity) dto.Capacity = Int(capacity, "عدد الشاغلين");
            return await SaveAsync(dto, id, () => service.CreateAsync(dto), () => service.UpdateAsync(dto), nameof(Position),
                () => Db.Positions.Where(p => p.Name == name && p.DepartmentId == departmentId).Select(p => p.Id).FirstAsync());
        }

        private async Task<ImportRowResult> LeaveTypeAsync(ImportRow row)
        {
            var name = Need(row, "name", "اسم النوع");
            var id = await Db.LeaveTypes.Where(t => t.Name == name).Select(t => (int?)t.Id).FirstOrDefaultAsync();
            var service = Service<ILeaveTypeService>();
            var dto = id is int existing ? (await service.GetForEditAsync(existing))! : new SaveLeaveTypeDto();
            dto.Name = name;
            if (row.Get("days") is { } days) dto.AnnualDays = Int(days, "الرصيد السنوي");
            else if (id is null) throw new ImportProblem("الرصيد السنوي مطلوب");
            if (row.Get("senior") is { } senior) dto.SeniorAnnualDays = Int(senior, "رصيد الأقدمية");
            if (row.Get("paid") is { } paid) dto.IsPaid = Bool(paid, "مدفوعة الأجر");
            if (row.Get("attachment") is { } attachment) dto.RequiresAttachment = Bool(attachment, "محتاجة مرفق");
            return await SaveAsync(dto, id, () => service.CreateAsync(dto), () => service.UpdateAsync(dto), nameof(LeaveType),
                () => Db.LeaveTypes.Where(t => t.Name == name).Select(t => t.Id).FirstAsync());
        }

        private async Task<ImportRowResult> HolidayAsync(ImportRow row)
        {
            var date = Date(Need(row, "date", "التاريخ"), "التاريخ");
            var name = Need(row, "name", "اسم العطلة");
            var id = await Db.PublicHolidays.Where(h => h.Date == date).Select(h => (int?)h.Id).FirstOrDefaultAsync();
            var service = Service<IPublicHolidayService>();
            var dto = new SavePublicHolidayDto { Id = id ?? 0, Date = date, Name = name };
            return await SaveAsync(dto, id, () => service.CreateAsync(dto), () => service.UpdateAsync(dto), nameof(PublicHoliday),
                () => Db.PublicHolidays.Where(h => h.Date == date).Select(h => h.Id).FirstAsync());
        }

        private async Task<ImportRowResult> ComponentAsync(ImportRow row)
        {
            var name = Need(row, "name", "اسم المكوّن");
            var id = await Db.SalaryComponents.Where(c => c.Name == name).Select(c => (int?)c.Id).FirstOrDefaultAsync();
            var service = Service<ISalaryComponentService>();
            var dto = id is int existing ? (await service.GetForEditAsync(existing))! : new SaveSalaryComponentDto();
            dto.Name = name;
            if (row.Get("type") is { } type) dto.Type = Enum<SalaryComponentType>(type, "النوع", Labels.Of);
            if (row.Get("method") is { } method) dto.CalcMethod = Enum<CalculationMethod>(method, "طريقة الحساب", Labels.Of);
            if (row.Get("taxable") is { } taxable) dto.IsTaxable = Bool(taxable, "خاضع للضريبة");
            return await SaveAsync(dto, id, () => service.CreateAsync(dto), () => service.UpdateAsync(dto), nameof(SalaryComponent),
                () => Db.SalaryComponents.Where(c => c.Name == name).Select(c => c.Id).FirstAsync());
        }

        private async Task<ImportRowResult> EmployeeAsync(ImportRow row)
        {
            var nationalId = Digits(Need(row, "nationalid", "الرقم القومي"));
            if (!NationalId.TryParse(nationalId, out var birthDate, out var gender))
                throw new ImportProblem("الرقم القومي مش صحيح");

            var id = await Db.Employees.Where(e => e.NationalId == nationalId).Select(e => (int?)e.Id).FirstOrDefaultAsync();
            var service = Service<IEmployeeService>();
            var dto = id is int existing ? (await service.GetForEditAsync(existing))! : new SaveEmployeeDto { InsuranceSalary = null };
            dto.NationalId = nationalId;
            dto.BirthDate = birthDate;
            dto.Gender = gender;

            dto.FullNameAr = row.Get("namear") ?? dto.FullNameAr ?? throw new ImportProblem("الاسم بالعربي مطلوب");
            dto.FullNameEn = row.Get("nameen") ?? dto.FullNameEn;
            if (row.Get("phone") is { } phone) dto.Phone = Phone(phone);
            dto.Email = row.Get("email") ?? dto.Email;
            dto.Address = row.Get("address") ?? dto.Address;
            if (row.Get("marital") is { } marital) dto.MaritalStatus = Enum<MaritalStatus>(marital, "الحالة الاجتماعية", Labels.Of);
            if (row.Get("hiredate") is { } hire) dto.HireDate = Date(hire, "تاريخ التعيين");
            if (row.Get("contract") is { } contract) dto.ContractType = Enum<ContractType>(contract, "نوع العقد", Labels.Of);
            if (row.Get("contractend") is { } end) dto.ContractEndDate = Date(end, "نهاية العقد");

            if (row.Get("position") is { } position)
            {
                dto.PositionId = await FindPositionAsync(position);
                var seat = await Db.Positions.Where(p => p.Id == dto.PositionId).Select(p => new { p.DepartmentId, p.JobTitleId }).FirstAsync();
                dto.DepartmentId = seat.DepartmentId;
                dto.JobTitleId = seat.JobTitleId;
            }
            if (row.Get("department") is { } department && row.Get("position") is null)
                dto.DepartmentId = await Db.Departments.Where(d => d.Name == department).Select(d => (int?)d.Id).FirstOrDefaultAsync()
                    ?? throw new ImportProblem($"الإدارة / القسم «{department}» مش موجود");
            if (row.Get("jobtitle") is { } job && row.Get("position") is null)
                dto.JobTitleId = await Db.JobTitles.Where(j => j.Name == job).Select(j => (int?)j.Id).FirstOrDefaultAsync()
                    ?? throw new ImportProblem($"المسمى «{job}» مش موجود");
            if (row.Get("manager") is { } manager)
            {
                var managerId = Digits(manager);
                dto.ManagerId = await Db.Employees.Where(e => e.NationalId == managerId).Select(e => (int?)e.Id).FirstOrDefaultAsync()
                    ?? throw new ImportProblem($"المدير ({managerId}) مش متسجل", wait: true);
            }
            if (row.Get("shift") is { } shift)
                dto.ShiftId = await Db.Shifts.Where(s => s.Name == shift).Select(s => (int?)s.Id).FirstOrDefaultAsync()
                    ?? throw new ImportProblem($"الوردية «{shift}» مش موجودة");

            if (row.Get("salary") is { } salary) dto.BasicSalary = Dec(salary, "المرتب الأساسي");
            if (row.Get("insurance") is { } insurance) dto.InsuranceSalary = Dec(insurance, "أجر الاشتراك");
            dto.BankName = row.Get("bank") ?? dto.BankName;
            dto.BankAccount = row.Get("bankaccount") ?? dto.BankAccount;
            dto.Iban = row.Get("iban")?.Replace(" ", "") ?? dto.Iban;
            dto.FingerprintId = row.Get("fingerprint") is { } fp ? Digits(fp) : dto.FingerprintId;
            if (row.Get("remote") is { } remote) dto.AllowRemoteAttendance = Bool(remote, "حضور من أي مكان");
            dto.Nationality = row.Get("nationality") ?? dto.Nationality;
            if (row.Get("religion") is { } religion) dto.Religion = Enum<Religion>(religion, "الديانة", Labels.Of);
            if (row.Get("military") is { } military) dto.MilitaryStatus = Enum<MilitaryStatus>(military, "الموقف من التجنيد", Labels.Of);
            dto.InsuranceNumber = row.Get("insuranceno") is { } ins ? Digits(ins) : dto.InsuranceNumber;
            dto.Governorate = row.Get("governorate") ?? dto.Governorate;
            dto.City = row.Get("city") ?? dto.City;
            dto.EmergencyContactName = row.Get("emergencyname") ?? dto.EmergencyContactName;
            if (row.Get("emergencyphone") is { } ephone) dto.EmergencyContactPhone = Phone(ephone);

            return await SaveAsync(dto, id,
                async () => await service.CreateAsync(dto, null),
                () => service.UpdateAsync(dto, null), nameof(Employee),
                () => Db.Employees.Where(e => e.NationalId == nationalId).Select(e => e.Id).FirstAsync());
        }

        // ---------- Saving ----------

        private async Task<ImportRowResult> SaveAsync(object dto, int? existingId, Func<Task<ServiceResult>> create,
            Func<Task<ServiceResult>> update, string entity, Func<Task<int>> findId)
        {
            // The same attribute checks the screens run (required, lengths, ranges, formats)
            var problems = new List<ValidationResult>();
            if (!Validator.TryValidateObject(dto, new ValidationContext(dto), problems, validateAllProperties: true))
                return Fail(string.Join(" · ", problems.Select(p => p.ErrorMessage).Distinct()));

            var result = existingId is null ? await create() : await update();
            if (!result.Succeeded)
                return Fail(result.Error ?? "مرفوض");

            Items.Add(new ImportBatchItem { EntityName = entity, EntityId = existingId ?? await findId(), Created = existingId is null });
            return existingId is null
                ? new ImportRowResult("", 0, "", ImportOutcome.Created, "جديد")
                : new ImportRowResult("", 0, "", ImportOutcome.Updated, "اتحدّث");
        }

        private static ImportRowResult Fail(string message) => new("", 0, "", ImportOutcome.Error, message);

        // "name" or "name / department" when the same name is used in more than one unit
        private async Task<int> FindPositionAsync(string text)
        {
            var parts = text.Split('/', 2, StringSplitOptions.TrimEntries);
            var query = Db.Positions.Where(p => p.Name == parts[0]);
            if (parts.Length == 2)
                query = query.Where(p => p.Department.Name == parts[1]);
            var ids = await query.Select(p => p.Id).Take(2).ToListAsync();
            return ids.Count switch
            {
                1 => ids[0],
                0 => throw new ImportProblem($"الموقع «{text}» مش موجود", wait: true),
                _ => throw new ImportProblem($"فيه أكتر من موقع اسمه «{parts[0]}». اكتبه: {parts[0]} / اسم القسم")
            };
        }
    }

    // ---------- Reading cell values ----------

    private sealed class ImportProblem(string message, bool wait = false) : Exception(message)
    {
        // The row points at a row of the same sheet that may come in later
        public bool Wait { get; } = wait;
    }

    private static string Need(ImportRow row, string key, string header)
        => row.Get(key) ?? throw new ImportProblem($"«{header}» مطلوب");

    private static string Latin(string s)
    {
        var chars = s.Trim().ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] is >= '٠' and <= '٩') chars[i] = (char)('0' + (chars[i] - '٠'));
            else if (chars[i] is >= '۰' and <= '۹') chars[i] = (char)('0' + (chars[i] - '۰'));
            else if (chars[i] == '٫') chars[i] = '.';
        }
        return new string(chars);
    }

    private static string Digits(string s) => new(Latin(s).Where(char.IsDigit).ToArray());

    // Excel drops the leading zero of 01xxxxxxxxx when the cell is a number
    private static string Phone(string s)
    {
        var digits = Digits(s);
        return digits.Length == 10 && digits[0] == '1' ? "0" + digits : digits;
    }

    private static decimal Dec(string s, string header)
        => decimal.TryParse(Latin(s).Replace(",", "").Replace("٬", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out var v)
            ? v : throw new ImportProblem($"«{header}» لازم يكون رقم");

    private static int Int(string s, string header)
        => decimal.TryParse(Latin(s), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) && v == Math.Floor(v)
            ? (int)v : throw new ImportProblem($"«{header}» لازم يكون رقم صحيح");

    private static readonly string[] DateFormats = ["yyyy-MM-dd", "yyyy/MM/dd", "d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "dd-MM-yyyy", "d.M.yyyy"];

    private static DateOnly Date(string s, string header)
        => DateOnly.TryParseExact(Latin(s), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d : throw new ImportProblem($"«{header}» مش تاريخ مفهوم (زي 2026-11-14)");

    private static readonly string[] TimeFormats = ["H:mm", "HH:mm", "H:mm:ss", "HH:mm:ss", "h:mm tt", "hh:mm tt"];

    private static TimeOnly Time(string s, string header)
        => TimeOnly.TryParseExact(Latin(s).Replace("ص", "AM").Replace("م", "PM"), TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)
            ? new TimeOnly(t.Hour, t.Minute) : throw new ImportProblem($"«{header}» مش وقت مفهوم (زي 09:00)");

    private static bool Bool(string s, string header) => s.Trim().ToLowerInvariant() switch
    {
        "أيوه" or "ايوه" or "نعم" or "اه" or "آه" or "yes" or "y" or "true" or "1" or "صح" => true,
        "لأ" or "لا" or "no" or "n" or "false" or "0" or "غلط" => false,
        _ => throw new ImportProblem($"«{header}» يا أيوه يا لأ")
    };

    // Arabic label as shown in the program, or the English name
    private static TEnum Enum<TEnum>(string s, string header, Func<TEnum, string> label) where TEnum : struct, Enum
    {
        var text = s.Trim();
        foreach (var value in System.Enum.GetValues<TEnum>())
            if (label(value) == text || value.ToString().Equals(text, StringComparison.OrdinalIgnoreCase))
                return value;
        throw new ImportProblem($"«{header}»: «{text}» مش من القيم المسموحة ({string.Join(" / ", System.Enum.GetValues<TEnum>().Select(label).Distinct())})");
    }
}
