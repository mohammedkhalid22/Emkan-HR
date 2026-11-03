using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.DAL.Repositories;
using HRSystem.Domain.Entities;
using HRSystem.Domain.Enums;
using HRSystem.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.BLL.Services;

public interface IEmployeeService
{
    Task<EmployeeAccessScope> GetAccessScopeAsync(string? userId, bool isAdminOrHr, bool isManager);
    Task<int?> GetEmployeeIdForUserAsync(string userId);

    Task<PagedResult<EmployeeListItemDto>> GetPagedAsync(EmployeeFilterDto filter, EmployeeAccessScope scope);
    Task<List<EmployeeListItemDto>> GetForExportAsync(EmployeeFilterDto filter, EmployeeAccessScope scope);
    Task<EmployeeDetailsDto?> GetDetailsAsync(int id, EmployeeAccessScope scope);
    Task<SaveEmployeeDto?> GetForEditAsync(int id);
    Task<string?> GetPhotoPathAsync(int id, EmployeeAccessScope scope);
    Task<int> CountActiveAsync();

    Task<List<LookupDto>> GetManagerLookupAsync(int? excludeId);
    Task<List<LookupDto>> GetShiftLookupAsync();

    Task<ServiceResult<int>> CreateAsync(SaveEmployeeDto dto, FileUpload? photo);
    Task<ServiceResult> UpdateAsync(SaveEmployeeDto dto, FileUpload? photo);
    Task<ServiceResult> TerminateAsync(TerminateEmployeeDto dto);
}

public class EmployeeService : IEmployeeService
{
    private const string CodePrefix = "EMP-";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericRepository<Employee> _employees;
    private readonly IFileStorage _fileStorage;
    private readonly UserManager<ApplicationUser> _userManager;

    public EmployeeService(IUnitOfWork unitOfWork, IFileStorage fileStorage, UserManager<ApplicationUser> userManager)
    {
        _unitOfWork = unitOfWork;
        _employees = unitOfWork.Repository<Employee>();
        _fileStorage = fileStorage;
        _userManager = userManager;
    }

    // ---------- Access ----------

    public async Task<EmployeeAccessScope> GetAccessScopeAsync(string? userId, bool isAdminOrHr, bool isManager)
    {
        if (isAdminOrHr)
            return new EmployeeAccessScope(SeeAll: true, OwnEmployeeId: null, SeeTeam: false);

        var ownId = userId is null ? null : await GetEmployeeIdForUserAsync(userId);
        return new EmployeeAccessScope(SeeAll: false, OwnEmployeeId: ownId, SeeTeam: isManager);
    }

    public Task<int?> GetEmployeeIdForUserAsync(string userId)
        => _employees.QueryNoTracking()
            .Where(e => e.UserId == userId)
            .Select(e => (int?)e.Id)
            .FirstOrDefaultAsync();

    private static IQueryable<Employee> ApplyScope(IQueryable<Employee> query, EmployeeAccessScope scope)
    {
        if (scope.SeeAll)
            return query;
        if (scope.OwnEmployeeId is not int ownId)
            return query.Where(_ => false);

        return scope.SeeTeam
            ? query.Where(e => e.Id == ownId || e.ManagerId == ownId)
            : query.Where(e => e.Id == ownId);
    }

    // ---------- Queries ----------

    public async Task<PagedResult<EmployeeListItemDto>> GetPagedAsync(EmployeeFilterDto filter, EmployeeAccessScope scope)
    {
        var query = BuildListQuery(filter, scope);
        var page = Math.Max(1, filter.Page);
        var total = await query.CountAsync();

        var items = await ProjectToListItem(query.OrderBy(e => e.Code))
            .Skip((page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return new PagedResult<EmployeeListItemDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = filter.PageSize
        };
    }

    public Task<List<EmployeeListItemDto>> GetForExportAsync(EmployeeFilterDto filter, EmployeeAccessScope scope)
        => ProjectToListItem(BuildListQuery(filter, scope).OrderBy(e => e.Code)).ToListAsync();

    private IQueryable<Employee> BuildListQuery(EmployeeFilterDto filter, EmployeeAccessScope scope)
    {
        var query = ApplyScope(_employees.QueryNoTracking(), scope);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(e => e.FullNameAr.Contains(term)
                || (e.FullNameEn != null && e.FullNameEn.Contains(term))
                || e.Code.Contains(term)
                || e.NationalId.Contains(term));
        }

        if (filter.DepartmentId is int departmentId)
            query = query.Where(e => e.DepartmentId == departmentId);

        if (filter.Status is EmployeeStatus status)
            query = query.Where(e => e.Status == status);

        return query;
    }

    private static IQueryable<EmployeeListItemDto> ProjectToListItem(IQueryable<Employee> query)
        => query.Select(e => new EmployeeListItemDto
        {
            Id = e.Id,
            Code = e.Code,
            FullNameAr = e.FullNameAr,
            NationalId = e.NationalId,
            DepartmentName = e.Department.Name,
            JobTitleName = e.JobTitle.Name,
            ManagerName = e.Manager != null ? e.Manager.FullNameAr : null,
            Phone = e.Phone,
            HireDate = e.HireDate,
            Status = e.Status,
            HasPhoto = e.PhotoPath != null
        });

    public async Task<EmployeeDetailsDto?> GetDetailsAsync(int id, EmployeeAccessScope scope)
    {
        var details = await ApplyScope(_employees.QueryNoTracking(), scope)
            .Where(e => e.Id == id)
            .Select(e => new EmployeeDetailsDto
            {
                Id = e.Id,
                Code = e.Code,
                FullNameAr = e.FullNameAr,
                FullNameEn = e.FullNameEn,
                NationalId = e.NationalId,
                BirthDate = e.BirthDate,
                Gender = e.Gender,
                MaritalStatus = e.MaritalStatus,
                Phone = e.Phone,
                Email = e.Email,
                Address = e.Address,
                HasPhoto = e.PhotoPath != null,
                HireDate = e.HireDate,
                ContractType = e.ContractType,
                ContractEndDate = e.ContractEndDate,
                Status = e.Status,
                TerminationDate = e.TerminationDate,
                TerminationReason = e.TerminationReason,
                DepartmentName = e.Department.Name,
                JobTitleName = e.JobTitle.Name,
                ManagerId = e.ManagerId,
                ManagerName = e.Manager != null ? e.Manager.FullNameAr : null,
                ShiftName = e.Shift != null ? e.Shift.Name : null,
                BasicSalary = e.BasicSalary,
                InsuranceSalary = e.InsuranceSalary,
                BankAccount = e.BankAccount,
                UserId = e.UserId,
                Documents = e.Documents
                    .OrderByDescending(d => d.CreatedAt)
                    .Select(d => new EmployeeDocumentDto
                    {
                        Id = d.Id,
                        DocumentType = d.DocumentType,
                        FileName = d.FilePath,
                        ExpiryDate = d.ExpiryDate,
                        UploadedAt = d.CreatedAt
                    }).ToList(),
                History = e.History
                    .OrderByDescending(h => h.EffectiveDate).ThenByDescending(h => h.Id)
                    .Select(h => new EmployeeHistoryDto
                    {
                        ChangeType = h.ChangeType,
                        OldValue = h.OldValue,
                        NewValue = h.NewValue,
                        EffectiveDate = h.EffectiveDate
                    }).ToList()
            })
            .FirstOrDefaultAsync();

        // Show the original file name, not the storage path ("{guid}_{original}")
        details?.Documents.ForEach(d => d.FileName = EmployeeDocumentService.OriginalFileName(d.FileName));
        return details;
    }

    public Task<SaveEmployeeDto?> GetForEditAsync(int id)
        => _employees.QueryNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new SaveEmployeeDto
            {
                Id = e.Id,
                FullNameAr = e.FullNameAr,
                FullNameEn = e.FullNameEn,
                NationalId = e.NationalId,
                BirthDate = e.BirthDate,
                Gender = e.Gender,
                MaritalStatus = e.MaritalStatus,
                Phone = e.Phone,
                Email = e.Email,
                Address = e.Address,
                DepartmentId = e.DepartmentId,
                JobTitleId = e.JobTitleId,
                ManagerId = e.ManagerId,
                ShiftId = e.ShiftId,
                AllowRemoteAttendance = e.AllowRemoteAttendance,
                HireDate = e.HireDate,
                ContractType = e.ContractType,
                ContractEndDate = e.ContractEndDate,
                BasicSalary = e.BasicSalary,
                InsuranceSalary = e.InsuranceSalary,
                BankAccount = e.BankAccount
            })
            .FirstOrDefaultAsync();

    public Task<string?> GetPhotoPathAsync(int id, EmployeeAccessScope scope)
        => ApplyScope(_employees.QueryNoTracking(), scope)
            .Where(e => e.Id == id)
            .Select(e => e.PhotoPath)
            .FirstOrDefaultAsync();

    public Task<int> CountActiveAsync()
        => _employees.QueryNoTracking().CountAsync(e => e.Status == EmployeeStatus.Active);

    public Task<List<LookupDto>> GetManagerLookupAsync(int? excludeId)
        => _employees.QueryNoTracking()
            .Where(e => e.Status == EmployeeStatus.Active && (excludeId == null || e.Id != excludeId))
            .OrderBy(e => e.FullNameAr)
            .Select(e => new LookupDto(e.Id, e.Code + " - " + e.FullNameAr))
            .ToListAsync();

    public Task<List<LookupDto>> GetShiftLookupAsync()
        => _unitOfWork.Repository<Shift>().QueryNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new LookupDto(s.Id, s.Name))
            .ToListAsync();

    // ---------- Commands ----------

    public async Task<ServiceResult<int>> CreateAsync(SaveEmployeeDto dto, FileUpload? photo)
    {
        var validation = await ValidateAsync(dto, photo);
        if (!validation.Succeeded)
            return ServiceResult<int>.From(validation);

        var employee = new Employee
        {
            Code = await GenerateCodeAsync(),
            Status = EmployeeStatus.Active
        };
        Map(dto, employee);

        await _employees.AddAsync(employee);
        employee.History.Add(new EmployeeHistory
        {
            ChangeType = "تعيين",
            NewValue = await DescribeJobAsync(employee.DepartmentId, employee.JobTitleId),
            EffectiveDate = employee.HireDate
        });

        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Two users saving at the same moment can get the same code; the unique index stops it
            return ServiceResult<int>.Failure("حصل تعارض أثناء الحفظ. جرب تاني");
        }

        if (photo is not null)
        {
            employee.PhotoPath = await _fileStorage.SaveAsync(photo, $"employees/{employee.Id}/photo");
            await _unitOfWork.SaveChangesAsync();
        }

        return ServiceResult<int>.Success(employee.Id);
    }

    public async Task<ServiceResult> UpdateAsync(SaveEmployeeDto dto, FileUpload? photo)
    {
        var employee = await _employees.GetByIdAsync(dto.Id);
        if (employee is null)
            return ServiceResult.Failure("الموظف مش موجود");

        var validation = await ValidateAsync(dto, photo);
        if (!validation.Succeeded)
            return validation;

        await RecordHistoryAsync(employee, dto);
        Map(dto, employee);

        if (photo is not null)
        {
            var oldPhoto = employee.PhotoPath;
            employee.PhotoPath = await _fileStorage.SaveAsync(photo, $"employees/{employee.Id}/photo");
            if (oldPhoto is not null)
                _fileStorage.Delete(oldPhoto);
        }

        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> TerminateAsync(TerminateEmployeeDto dto)
    {
        var employee = await _employees.GetByIdAsync(dto.EmployeeId);
        if (employee is null)
            return ServiceResult.Failure("الموظف مش موجود");
        if (employee.Status == EmployeeStatus.Terminated)
            return ServiceResult.Failure("خدمة الموظف منتهية بالفعل");

        var date = dto.TerminationDate!.Value;
        if (date < employee.HireDate)
            return ServiceResult.Failure("تاريخ إنهاء الخدمة لازم يكون بعد تاريخ التعيين", nameof(dto.TerminationDate));

        var subordinates = await _employees.QueryNoTracking()
            .CountAsync(e => e.ManagerId == employee.Id && e.Status == EmployeeStatus.Active);
        if (subordinates > 0)
            return ServiceResult.Failure($"الموظف ده مدير مباشر لـ {subordinates} موظف. غيّر مديرهم الأول");

        employee.Status = EmployeeStatus.Terminated;
        employee.TerminationDate = date;
        employee.TerminationReason = dto.Reason.Trim();
        employee.History.Add(new EmployeeHistory
        {
            ChangeType = "إنهاء خدمة",
            NewValue = dto.Reason.Trim(),
            EffectiveDate = date
        });

        // A department managed by this employee is left without a manager
        var managedDepartments = await _unitOfWork.Repository<Department>().Query()
            .Where(d => d.ManagerId == employee.Id).ToListAsync();
        managedDepartments.ForEach(d => d.ManagerId = null);

        await _unitOfWork.SaveChangesAsync();

        if (employee.UserId is not null && await _userManager.FindByIdAsync(employee.UserId) is { } user)
        {
            user.IsActive = false;
            await _userManager.UpdateAsync(user);
            await _userManager.UpdateSecurityStampAsync(user);
        }

        return ServiceResult.Success();
    }

    // ---------- Helpers ----------

    private static void Map(SaveEmployeeDto dto, Employee employee)
    {
        employee.FullNameAr = dto.FullNameAr.Trim();
        employee.FullNameEn = dto.FullNameEn?.Trim();
        employee.NationalId = dto.NationalId.Trim();
        employee.BirthDate = dto.BirthDate!.Value;
        employee.Gender = dto.Gender!.Value;
        employee.MaritalStatus = dto.MaritalStatus;
        employee.Phone = dto.Phone?.Trim();
        employee.Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
        employee.Address = dto.Address?.Trim();
        employee.DepartmentId = dto.DepartmentId!.Value;
        employee.JobTitleId = dto.JobTitleId!.Value;
        employee.ManagerId = dto.ManagerId;
        employee.ShiftId = dto.ShiftId;
        employee.AllowRemoteAttendance = dto.AllowRemoteAttendance;
        employee.HireDate = dto.HireDate!.Value;
        employee.ContractType = dto.ContractType!.Value;
        employee.ContractEndDate = dto.ContractEndDate;
        employee.BasicSalary = dto.BasicSalary!.Value;
        employee.InsuranceSalary = dto.InsuranceSalary ?? dto.BasicSalary!.Value;
        employee.BankAccount = dto.BankAccount?.Trim();
    }

    private async Task<string> GenerateCodeAsync()
    {
        // Include soft-deleted rows so a code is never reused
        var codes = await _employees.QueryNoTracking().IgnoreQueryFilters()
            .Where(e => e.Code.StartsWith(CodePrefix))
            .Select(e => e.Code)
            .ToListAsync();

        var max = codes
            .Select(c => int.TryParse(c[CodePrefix.Length..], out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{CodePrefix}{max + 1:D4}";
    }

    private async Task<ServiceResult> ValidateAsync(SaveEmployeeDto dto, FileUpload? photo)
    {
        // National ID must be valid and agree with the birth date and gender typed in
        if (!NationalId.TryParse(dto.NationalId, out var idBirthDate, out var idGender))
            return ServiceResult.Failure("الرقم القومي مش صحيح", nameof(dto.NationalId));
        if (dto.BirthDate != idBirthDate)
            return ServiceResult.Failure($"تاريخ الميلاد مش مطابق للرقم القومي ({idBirthDate:yyyy-MM-dd})", nameof(dto.BirthDate));
        if (dto.Gender != idGender)
            return ServiceResult.Failure("النوع مش مطابق للرقم القومي", nameof(dto.Gender));

        if (await _employees.QueryNoTracking().AnyAsync(e => e.NationalId == dto.NationalId && e.Id != dto.Id))
            return ServiceResult.Failure("الرقم القومي ده متسجل لموظف تاني", nameof(dto.NationalId));

        if (!string.IsNullOrWhiteSpace(dto.Email)
            && await _employees.QueryNoTracking().AnyAsync(e => e.Email == dto.Email.Trim() && e.Id != dto.Id))
            return ServiceResult.Failure("البريد الإلكتروني ده متسجل لموظف تاني", nameof(dto.Email));

        var hireDate = dto.HireDate!.Value;
        if (idBirthDate.AddYears(18) > hireDate)
            return ServiceResult.Failure("سن الموظف لازم يكون 18 سنة أو أكتر وقت التعيين", nameof(dto.HireDate));
        if (hireDate > DateOnly.FromDateTime(DateTime.Today).AddMonths(3))
            return ServiceResult.Failure("تاريخ التعيين بعيد أوي في المستقبل", nameof(dto.HireDate));

        if (dto.ContractType == ContractType.Temporary && dto.ContractEndDate is null)
            return ServiceResult.Failure("العقد المؤقت لازم يكون ليه تاريخ انتهاء", nameof(dto.ContractEndDate));
        if (dto.ContractEndDate is DateOnly end && end <= hireDate)
            return ServiceResult.Failure("تاريخ انتهاء العقد لازم يكون بعد تاريخ التعيين", nameof(dto.ContractEndDate));

        if (!await _unitOfWork.Repository<Department>().QueryNoTracking().AnyAsync(d => d.Id == dto.DepartmentId))
            return ServiceResult.Failure("القسم مش موجود", nameof(dto.DepartmentId));

        var jobTitle = await _unitOfWork.Repository<JobTitle>().QueryNoTracking()
            .FirstOrDefaultAsync(j => j.Id == dto.JobTitleId);
        if (jobTitle is null)
            return ServiceResult.Failure("المسمى الوظيفي مش موجود", nameof(dto.JobTitleId));

        var salary = dto.BasicSalary!.Value;
        if (jobTitle.MinSalary is decimal min && salary < min)
            return ServiceResult.Failure($"المرتب أقل من الحد الأدنى للمسمى ({min:N0})", nameof(dto.BasicSalary));
        if (jobTitle.MaxSalary is decimal max && salary > max)
            return ServiceResult.Failure($"المرتب أكبر من الحد الأقصى للمسمى ({max:N0})", nameof(dto.BasicSalary));

        if (dto.ShiftId is int shiftId
            && !await _unitOfWork.Repository<Shift>().QueryNoTracking().AnyAsync(s => s.Id == shiftId))
            return ServiceResult.Failure("الوردية مش موجودة", nameof(dto.ShiftId));

        if (dto.ManagerId is int managerId)
        {
            if (managerId == dto.Id)
                return ServiceResult.Failure("الموظف مينفعش يبقى مدير نفسه", nameof(dto.ManagerId));

            var managerActive = await _employees.QueryNoTracking()
                .AnyAsync(e => e.Id == managerId && e.Status == EmployeeStatus.Active);
            if (!managerActive)
                return ServiceResult.Failure("المدير لازم يكون موظف نشط", nameof(dto.ManagerId));

            if (dto.Id != 0 && await ManagerChainContainsAsync(managerId, dto.Id))
                return ServiceResult.Failure("مينفعش تختار مدير هو أصلًا تحت الموظف ده", nameof(dto.ManagerId));
        }

        if (photo is not null && FileRules.Validate(photo, FileRules.PhotoExtensions) is string photoError)
            return ServiceResult.Failure(photoError, "Photo");

        return ServiceResult.Success();
    }

    // Walks up from managerId; reaching employeeId means a reporting loop
    private async Task<bool> ManagerChainContainsAsync(int managerId, int employeeId)
    {
        var managers = await _employees.QueryNoTracking()
            .Where(e => e.ManagerId != null)
            .ToDictionaryAsync(e => e.Id, e => e.ManagerId);

        int? current = managerId;
        var visited = new HashSet<int>();
        while (current is int id && visited.Add(id))
        {
            if (id == employeeId)
                return true;
            current = managers.GetValueOrDefault(id);
        }

        return false;
    }

    private async Task RecordHistoryAsync(Employee employee, SaveEmployeeDto dto)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        void Add(string type, string? oldValue, string? newValue) => employee.History.Add(new EmployeeHistory
        {
            ChangeType = type,
            OldValue = oldValue,
            NewValue = newValue,
            EffectiveDate = today
        });

        if (employee.DepartmentId != dto.DepartmentId)
            Add("نقل قسم", await DepartmentNameAsync(employee.DepartmentId), await DepartmentNameAsync(dto.DepartmentId!.Value));

        if (employee.JobTitleId != dto.JobTitleId)
            Add("تغيير المسمى", await JobTitleNameAsync(employee.JobTitleId), await JobTitleNameAsync(dto.JobTitleId!.Value));

        if (employee.BasicSalary != dto.BasicSalary)
            Add("تغيير المرتب", employee.BasicSalary.ToString("N2"), dto.BasicSalary!.Value.ToString("N2"));

        if (employee.ManagerId != dto.ManagerId)
            Add("تغيير المدير", await EmployeeNameAsync(employee.ManagerId), await EmployeeNameAsync(dto.ManagerId));
    }

    private async Task<string> DescribeJobAsync(int departmentId, int jobTitleId)
        => $"{await JobTitleNameAsync(jobTitleId)} - {await DepartmentNameAsync(departmentId)}";

    private Task<string?> DepartmentNameAsync(int id)
        => _unitOfWork.Repository<Department>().QueryNoTracking().Where(d => d.Id == id).Select(d => d.Name).FirstOrDefaultAsync();

    private Task<string?> JobTitleNameAsync(int id)
        => _unitOfWork.Repository<JobTitle>().QueryNoTracking().Where(j => j.Id == id).Select(j => j.Name).FirstOrDefaultAsync();

    private async Task<string?> EmployeeNameAsync(int? id)
        => id is null ? "بدون" : await _employees.QueryNoTracking().Where(e => e.Id == id).Select(e => e.FullNameAr).FirstOrDefaultAsync();
}
