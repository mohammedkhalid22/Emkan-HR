using HRSystem.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace HRSystem.Domain.Identity;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = null!;
    public bool IsActive { get; set; } = true;

    public Employee? Employee { get; set; }
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string HR = "HR";
    public const string Manager = "Manager";
    public const string Employee = "Employee";

    public static readonly string[] All = [Admin, HR, Manager, Employee];

    // Common combinations used in [Authorize(Roles = ...)]
    public const string AdminOrHR = Admin + "," + HR;
}
