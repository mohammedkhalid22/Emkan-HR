using System.Security.Claims;
using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.BLL.Services;
using HRSystem.Domain.Identity;

namespace HRSystem.Web.Extensions;

public static class EmployeeScopeExtensions
{
    public static bool IsAdminOrHR(this ClaimsPrincipal user)
        => user.IsInRole(Roles.Admin) || user.IsInRole(Roles.HR);

    // Which employees the logged-in user is allowed to see
    public static Task<EmployeeAccessScope> GetScopeAsync(this IEmployeeService service, ClaimsPrincipal user)
        => service.GetAccessScopeAsync(
            user.FindFirstValue(ClaimTypes.NameIdentifier),
            user.IsAdminOrHR(),
            user.IsInRole(Roles.Manager));

    // Who is acting on leave requests: their employee record and which approval steps they can take
    public static async Task<ApproverContext> GetApproverAsync(this IEmployeeService service, ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var employeeId = userId is null ? null : await service.GetEmployeeIdForUserAsync(userId);
        return new ApproverContext(userId, employeeId, user.IsAdminOrHR(), user.IsInRole(Roles.Manager));
    }

    public static FileUpload? ToFileUpload(this IFormFile? file)
        => file is null || file.Length == 0 ? null : new FileUpload(file.OpenReadStream(), file.FileName, file.Length);
}
