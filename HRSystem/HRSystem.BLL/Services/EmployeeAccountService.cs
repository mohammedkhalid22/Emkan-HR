using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.DAL.Repositories;
using HRSystem.Domain.Entities;
using HRSystem.Domain.Enums;
using HRSystem.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.BLL.Services;

public interface IEmployeeAccountService
{
    Task<EmployeeAccountDto?> GetAsync(string userId);
    Task<ServiceResult> CreateAsync(CreateAccountDto dto);
    Task<ServiceResult> ChangeRoleAsync(int employeeId, string role);
    Task<ServiceResult> SetActiveAsync(int employeeId, bool isActive);
    Task<ServiceResult> ResetPasswordAsync(int employeeId, string newPassword);
}

public class EmployeeAccountService : IEmployeeAccountService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;

    public EmployeeAccountService(IUnitOfWork unitOfWork, UserManager<ApplicationUser> userManager)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
    }

    public async Task<EmployeeAccountDto?> GetAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return null;

        return new EmployeeAccountDto
        {
            UserId = user.Id,
            Email = user.Email ?? user.UserName ?? "",
            Role = (await _userManager.GetRolesAsync(user)).FirstOrDefault(),
            IsActive = user.IsActive,
            IsLockedOut = await _userManager.IsLockedOutAsync(user)
        };
    }

    public async Task<ServiceResult> CreateAsync(CreateAccountDto dto)
    {
        if (!Roles.All.Contains(dto.Role))
            return ServiceResult.Failure("الدور مش صحيح", nameof(dto.Role));

        var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(dto.EmployeeId);
        if (employee is null)
            return ServiceResult.Failure("الموظف مش موجود");
        if (employee.UserId is not null)
            return ServiceResult.Failure("الموظف ليه حساب بالفعل");
        if (employee.Status != EmployeeStatus.Active)
            return ServiceResult.Failure("مينفعش تعمل حساب لموظف مش نشط");

        var email = dto.Email.Trim();
        if (await _userManager.FindByEmailAsync(email) is not null)
            return ServiceResult.Failure("البريد ده مستخدم في حساب تاني", nameof(dto.Email));

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = employee.FullNameAr
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return ServiceResult.Failure(DescribeErrors(result), nameof(dto.Password));

        await _userManager.AddToRoleAsync(user, dto.Role);

        employee.UserId = user.Id;
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> ChangeRoleAsync(int employeeId, string role)
    {
        if (!Roles.All.Contains(role))
            return ServiceResult.Failure("الدور مش صحيح");

        var user = await FindUserAsync(employeeId);
        if (user is null)
            return ServiceResult.Failure("الموظف ماعندوش حساب");

        await _userManager.RemoveFromRolesAsync(user, await _userManager.GetRolesAsync(user));
        await _userManager.AddToRoleAsync(user, role);

        // Forces the new role to apply on the user's next request
        await _userManager.UpdateSecurityStampAsync(user);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> SetActiveAsync(int employeeId, bool isActive)
    {
        var employee = await _unitOfWork.Repository<Employee>().QueryNoTracking()
            .FirstOrDefaultAsync(e => e.Id == employeeId);
        if (employee?.UserId is null)
            return ServiceResult.Failure("الموظف ماعندوش حساب");
        if (isActive && employee.Status != EmployeeStatus.Active)
            return ServiceResult.Failure("مينفعش تفعّل حساب موظف خدمته منتهية");

        var user = await _userManager.FindByIdAsync(employee.UserId);
        if (user is null)
            return ServiceResult.Failure("الحساب مش موجود");

        user.IsActive = isActive;
        await _userManager.UpdateAsync(user);
        await _userManager.UpdateSecurityStampAsync(user);

        if (isActive)
            await _userManager.SetLockoutEndDateAsync(user, null);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> ResetPasswordAsync(int employeeId, string newPassword)
    {
        var user = await FindUserAsync(employeeId);
        if (user is null)
            return ServiceResult.Failure("الموظف ماعندوش حساب");

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        return result.Succeeded ? ServiceResult.Success() : ServiceResult.Failure(DescribeErrors(result));
    }

    private async Task<ApplicationUser?> FindUserAsync(int employeeId)
    {
        var userId = await _unitOfWork.Repository<Employee>().QueryNoTracking()
            .Where(e => e.Id == employeeId)
            .Select(e => e.UserId)
            .FirstOrDefaultAsync();

        return userId is null ? null : await _userManager.FindByIdAsync(userId);
    }

    // Identity's messages are English; translate the common password ones
    private static string DescribeErrors(IdentityResult result)
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
