using System.ComponentModel.DataAnnotations;
using HRSystem.BLL.Common;
using HRSystem.DAL.Repositories;
using HRSystem.Domain.Entities;
using HRSystem.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.BLL.Services;

public class UserDto
{
    public string Id { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? Role { get; set; }
    public bool IsActive { get; set; }
    public bool IsLockedOut { get; set; }
    public int? EmployeeId { get; set; }
    public string? EmployeeCode { get; set; }
}

public class CreateUserDto
{
    [Display(Name = "الاسم")]
    [Required(ErrorMessage = "{0} مطلوب")]
    [StringLength(150)]
    public string FullName { get; set; } = null!;

    [Display(Name = "البريد الإلكتروني")]
    [Required(ErrorMessage = "{0} مطلوب")]
    [EmailAddress(ErrorMessage = "{0} مش صحيح")]
    public string Email { get; set; } = null!;

    [Display(Name = "الدور")]
    [Required(ErrorMessage = "{0} مطلوب")]
    public string Role { get; set; } = Roles.HR;

    [Display(Name = "كلمة السر")]
    [Required(ErrorMessage = "{0} مطلوبة")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "{0} لازم تكون {2} حروف على الأقل")]
    public string Password { get; set; } = null!;
}

// All login accounts, including ones not linked to an employee (e.g. a second admin or an outside accountant)
public interface IUserService
{
    Task<List<UserDto>> GetAllAsync();
    Task<ServiceResult> CreateAsync(CreateUserDto dto);
    Task<ServiceResult> ChangeRoleAsync(string userId, string role, string? currentUserId);
    Task<ServiceResult> SetActiveAsync(string userId, bool isActive, string? currentUserId);
    Task<ServiceResult> ResetPasswordAsync(string userId, string newPassword);
    Task<ServiceResult> UnlockAsync(string userId);
}

public class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;

    public UserService(UserManager<ApplicationUser> userManager, IUnitOfWork unitOfWork)
    {
        _userManager = userManager;
        _unitOfWork = unitOfWork;
    }

    public async Task<List<UserDto>> GetAllAsync()
    {
        var users = await _userManager.Users.AsNoTracking().OrderBy(u => u.Email).ToListAsync();
        var employees = await _unitOfWork.Repository<Employee>().QueryNoTracking()
            .Where(e => e.UserId != null)
            .ToDictionaryAsync(e => e.UserId!, e => new { e.Id, e.Code });

        var result = new List<UserDto>();
        foreach (var user in users)
        {
            employees.TryGetValue(user.Id, out var employee);
            result.Add(new UserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? user.UserName ?? "",
                Role = (await _userManager.GetRolesAsync(user)).FirstOrDefault(),
                IsActive = user.IsActive,
                IsLockedOut = user.LockoutEnd > DateTimeOffset.UtcNow,
                EmployeeId = employee?.Id,
                EmployeeCode = employee?.Code
            });
        }
        return result;
    }

    public async Task<ServiceResult> CreateAsync(CreateUserDto dto)
    {
        if (!Roles.All.Contains(dto.Role))
            return ServiceResult.Failure("الدور مش صحيح", nameof(dto.Role));
        if (await _userManager.FindByEmailAsync(dto.Email.Trim()) is not null)
            return ServiceResult.Failure("البريد ده مستخدم في حساب تاني", nameof(dto.Email));

        var user = new ApplicationUser
        {
            UserName = dto.Email.Trim(),
            Email = dto.Email.Trim(),
            EmailConfirmed = true,
            FullName = dto.FullName.Trim()
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return ServiceResult.Failure(Describe(result), nameof(dto.Password));

        await _userManager.AddToRoleAsync(user, dto.Role);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> ChangeRoleAsync(string userId, string role, string? currentUserId)
    {
        if (!Roles.All.Contains(role))
            return ServiceResult.Failure("الدور مش صحيح");

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return ServiceResult.Failure("الحساب مش موجود");

        if (role != Roles.Admin && await IsLastActiveAdminAsync(user))
            return ServiceResult.Failure("ده آخر مدير نظام نشط. لازم يفضل واحد على الأقل");
        if (userId == currentUserId && role != Roles.Admin)
            return ServiceResult.Failure("مينفعش تشيل صلاحية مدير النظام من نفسك");

        await _userManager.RemoveFromRolesAsync(user, await _userManager.GetRolesAsync(user));
        await _userManager.AddToRoleAsync(user, role);
        await _userManager.UpdateSecurityStampAsync(user);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> SetActiveAsync(string userId, bool isActive, string? currentUserId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return ServiceResult.Failure("الحساب مش موجود");

        if (!isActive)
        {
            if (userId == currentUserId)
                return ServiceResult.Failure("مينفعش توقف حسابك انت");
            if (await IsLastActiveAdminAsync(user))
                return ServiceResult.Failure("ده آخر مدير نظام نشط. لازم يفضل واحد على الأقل");
        }
        else
        {
            var employeeTerminated = await _unitOfWork.Repository<Employee>().QueryNoTracking()
                .AnyAsync(e => e.UserId == userId && e.Status == Domain.Enums.EmployeeStatus.Terminated);
            if (employeeTerminated)
                return ServiceResult.Failure("مينفعش تفعّل حساب موظف خدمته منتهية");
        }

        user.IsActive = isActive;
        await _userManager.UpdateAsync(user);
        await _userManager.UpdateSecurityStampAsync(user);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> ResetPasswordAsync(string userId, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return ServiceResult.Failure("الحساب مش موجود");

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword ?? "");
        return result.Succeeded ? ServiceResult.Success() : ServiceResult.Failure(Describe(result));
    }

    public async Task<ServiceResult> UnlockAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return ServiceResult.Failure("الحساب مش موجود");

        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);
        return ServiceResult.Success();
    }

    private async Task<bool> IsLastActiveAdminAsync(ApplicationUser user)
    {
        if (!user.IsActive || !await _userManager.IsInRoleAsync(user, Roles.Admin))
            return false;

        var admins = await _userManager.GetUsersInRoleAsync(Roles.Admin);
        return admins.Count(a => a.IsActive) <= 1;
    }

    private static string Describe(IdentityResult result)
        => string.Join("، ", result.Errors.Select(e => e.Code switch
        {
            "PasswordTooShort" => "كلمة السر لازم تكون 8 حروف على الأقل",
            "PasswordRequiresDigit" => "كلمة السر لازم يكون فيها رقم",
            "PasswordRequiresLower" => "كلمة السر لازم يكون فيها حرف إنجليزي صغير",
            "PasswordRequiresUpper" => "كلمة السر لازم يكون فيها حرف إنجليزي كبير",
            "DuplicateUserName" or "DuplicateEmail" => "البريد ده مستخدم في حساب تاني",
            _ => e.Description
        }));
}
