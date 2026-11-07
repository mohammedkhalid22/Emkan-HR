using System.Linq.Expressions;
using System.Text.Json;
using HRSystem.DAL.Interfaces;
using HRSystem.Domain.Common;
using HRSystem.Domain.Entities;
using HRSystem.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace HRSystem.DAL.Data;

public class HRDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly ICurrentUserService? _currentUser;

    public HRDbContext(DbContextOptions<HRDbContext> options, ICurrentUserService? currentUser = null)
        : base(options)
    {
        _currentUser = currentUser;
    }

    // Organization
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<JobTitle> JobTitles => Set<JobTitle>();
    public DbSet<Position> Positions => Set<Position>();

    // Employees
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();
    public DbSet<EmployeeHistory> EmployeeHistory => Set<EmployeeHistory>();
    public DbSet<EmployeeFamilyMember> EmployeeFamilyMembers => Set<EmployeeFamilyMember>();
    public DbSet<EmployeeQualification> EmployeeQualifications => Set<EmployeeQualification>();
    public DbSet<EmployeeExperience> EmployeeExperiences => Set<EmployeeExperience>();
    public DbSet<CvAnalysis> CvAnalyses => Set<CvAnalysis>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<Notification> Notifications => Set<Notification>();

    // Leaves
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<PublicHoliday> PublicHolidays => Set<PublicHoliday>();

    // Attendance
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<AttendanceRequest> AttendanceRequests => Set<AttendanceRequest>();
    public DbSet<AttendanceDevice> AttendanceDevices => Set<AttendanceDevice>();
    public DbSet<AttendancePunch> AttendancePunches => Set<AttendancePunch>();

    // Payroll
    public DbSet<SalaryComponent> SalaryComponents => Set<SalaryComponent>();
    public DbSet<EmployeeSalaryComponent> EmployeeSalaryComponents => Set<EmployeeSalaryComponent>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<PayrollAdjustment> PayrollAdjustments => Set<PayrollAdjustment>();
    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<PayrollItem> PayrollItems => Set<PayrollItem>();
    public DbSet<TaxBracket> TaxBrackets => Set<TaxBracket>();

    // System
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(HRDbContext).Assembly);

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            // Never cascade deletes between our tables: history must not disappear by accident
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                foreach (var fk in entityType.GetForeignKeys())
                    fk.DeleteBehavior = DeleteBehavior.Restrict;

                builder.Entity(entityType.ClrType).HasQueryFilter(BuildSoftDeleteFilter(entityType.ClrType));
            }
        }
    }

    // e => !((BaseEntity)e).IsDeleted
    private static LambdaExpression BuildSoftDeleteFilter(Type type)
    {
        var parameter = Expression.Parameter(type, "e");
        var property = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
        return Expression.Lambda(Expression.Not(property), parameter);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var userId = _currentUser?.UserId;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = userId;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    break;
                case EntityState.Deleted:
                    // Soft delete: turn the DELETE into an UPDATE
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    break;
            }
        }

        var pendingAudits = CollectAuditEntries(userId, now);
        var result = await base.SaveChangesAsync(cancellationToken);

        if (pendingAudits.Count > 0)
        {
            // Ids of added rows are only known after the first save
            foreach (var (entry, log) in pendingAudits.Where(p => p.Log.Action == "Insert" && p.Entry.Metadata.FindProperty("Id") is not null))
                log.EntityId = entry.Property("Id").CurrentValue?.ToString() ?? "";

            AuditLogs.AddRange(pendingAudits.Select(p => p.Log));
            await base.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    // Account fields worth a trace; the rest (stamps, failed-login counters, tokens) is noise
    private static readonly HashSet<string> AuditedUserFields =
        [nameof(ApplicationUser.FullName), nameof(ApplicationUser.Email), nameof(ApplicationUser.IsActive),
         nameof(ApplicationUser.TwoFactorEnabled), nameof(ApplicationUser.LockoutEnd), nameof(ApplicationUser.PasswordHash)];

    private List<(EntityEntry Entry, AuditLog Log)> CollectAuditEntries(string? userId, DateTime now)
    {
        var audits = new List<(EntityEntry, AuditLog)>();

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            // Business data, settings, accounts and role grants. Notifications are messages:
            // auditing them would flood the log
            Func<string, bool>? keep = entry.Entity switch
            {
                Notification => null,
                BaseEntity => name => name is not (nameof(BaseEntity.CreatedAt) or nameof(BaseEntity.CreatedBy)
                    or nameof(BaseEntity.UpdatedAt) or nameof(BaseEntity.UpdatedBy)),
                SystemSetting => name => name == nameof(SystemSetting.Value),
                ApplicationUser => AuditedUserFields.Contains,
                IdentityUserRole<string> => name => name == nameof(IdentityUserRole<string>.RoleId),
                _ => null
            };
            if (keep is null)
                continue;

            var action = entry.State == EntityState.Added ? "Insert"
                : entry.State == EntityState.Deleted || entry.Entity is BaseEntity { IsDeleted: true } ? "Delete"
                : "Update";

            var oldValues = new Dictionary<string, object?>();
            var newValues = new Dictionary<string, object?>();

            foreach (var property in entry.Properties)
            {
                var name = property.Metadata.Name;
                if (!keep(name))
                    continue;

                // Never copy a password hash into the log, only the fact that it changed
                object? Value(object? v) => name == nameof(ApplicationUser.PasswordHash) ? (v is null ? null : "***") : v;

                if (entry.State == EntityState.Added)
                    newValues[name] = Value(property.CurrentValue);
                else if (entry.State == EntityState.Deleted)
                    oldValues[name] = Value(property.OriginalValue);
                else if (property.IsModified && !Equals(property.OriginalValue, property.CurrentValue))
                {
                    oldValues[name] = Value(property.OriginalValue);
                    newValues[name] = Value(property.CurrentValue);
                }
            }

            // A sign-in only touches counters and stamps: nothing to record
            if (action == "Update" && newValues.Count == 0)
                continue;

            audits.Add((entry, new AuditLog
            {
                UserId = userId,
                Action = action,
                EntityName = entry.Entity is IdentityUserRole<string> ? "UserRole" : entry.Metadata.ClrType.Name,
                EntityId = (entry.Entity is IdentityUserRole<string> role ? role.UserId : entry.Property("Id").CurrentValue?.ToString()) ?? "",
                OldValues = oldValues.Count == 0 ? null : JsonSerializer.Serialize(oldValues),
                NewValues = newValues.Count == 0 ? null : JsonSerializer.Serialize(newValues),
                Timestamp = now
            }));
        }

        return audits;
    }
}
