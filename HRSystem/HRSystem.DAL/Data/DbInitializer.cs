using HRSystem.Domain.Entities;
using HRSystem.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.DAL.Data;

public class SeedAdminOptions
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string FullName { get; set; } = "مدير النظام";
}

public static class DbInitializer
{
    public static async Task SeedAsync(
        HRDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        SeedAdminOptions? admin)
    {
        await context.Database.MigrateAsync();

        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        if (admin is not null && await userManager.FindByEmailAsync(admin.Email) is null)
        {
            var user = new ApplicationUser
            {
                UserName = admin.Email,
                Email = admin.Email,
                EmailConfirmed = true,
                FullName = admin.FullName
            };

            var result = await userManager.CreateAsync(user, admin.Password);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    "Seeding admin failed: " + string.Join(", ", result.Errors.Select(e => e.Description)));

            await userManager.AddToRoleAsync(user, Roles.Admin);
        }

        if (!await context.LeaveTypes.AnyAsync())
        {
            context.LeaveTypes.AddRange(
                new LeaveType { Name = "سنوية", AnnualDays = 21, SeniorAnnualDays = 30, IsPaid = true },
                new LeaveType { Name = "عارضة", AnnualDays = 7, IsPaid = true },
                new LeaveType { Name = "مرضية", AnnualDays = 0, IsPaid = true, RequiresAttachment = true },
                new LeaveType { Name = "بدون أجر", AnnualDays = 0, IsPaid = false });
        }

        if (!await context.Shifts.AnyAsync())
        {
            context.Shifts.Add(new Shift
            {
                Name = "الوردية الصباحية",
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(17, 0),
                GraceMinutes = 15
            });
        }

        // Default values from the business rules (section 8); editable later from the settings screen
        var defaultSettings = new Dictionary<string, (string Value, string Description)>
        {
            ["WeekendDays"] = ("Friday,Saturday", "أيام الإجازة الأسبوعية"),
            ["SalaryDaysPerMonth"] = ("30", "أجر اليوم = الأساسي ÷ هذا الرقم"),
            ["WorkHoursPerDay"] = ("8", "عدد ساعات العمل في اليوم"),
            ["OvertimeRate"] = ("1.35", "معامل الأوفر تايم"),
            ["OvertimeMinimumMinutes"] = ("30", "أقل مدة تتحسب أوفر تايم"),
            ["InsuranceEmployeeRate"] = ("0.11", "نسبة حصة الموظف في التأمينات"),
            ["InsuranceCompanyRate"] = ("0.1875", "نسبة حصة الشركة في التأمينات"),
            ["InsuranceMinSalary"] = ("0", "الحد الأدنى لأجر الاشتراك — يحدث كل يناير"),
            ["InsuranceMaxSalary"] = ("0", "الحد الأقصى لأجر الاشتراك — يحدث كل يناير"),
            ["TaxPersonalExemption"] = ("0", "الإعفاء الشخصي السنوي — يراجع مع المحاسب")
        };

        var existingKeys = await context.SystemSettings.Select(s => s.Key).ToListAsync();
        foreach (var (key, (value, description)) in defaultSettings)
        {
            if (!existingKeys.Contains(key))
                context.SystemSettings.Add(new SystemSetting { Key = key, Value = value, Description = description });
        }

        await context.SaveChangesAsync();
    }
}
