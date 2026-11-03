using HRSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRSystem.DAL.Configurations;

public class SalaryComponentConfiguration : IEntityTypeConfiguration<SalaryComponent>
{
    public void Configure(EntityTypeBuilder<SalaryComponent> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(c => c.Name).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class EmployeeSalaryComponentConfiguration : IEntityTypeConfiguration<EmployeeSalaryComponent>
{
    public void Configure(EntityTypeBuilder<EmployeeSalaryComponent> builder)
    {
        builder.HasIndex(c => new { c.EmployeeId, c.SalaryComponentId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Loans_Amount", "[Amount] > 0");
            t.HasCheckConstraint("CK_Loans_Installments", "[InstallmentsCount] >= 1");
        });
    }
}

public class PayrollAdjustmentConfiguration : IEntityTypeConfiguration<PayrollAdjustment>
{
    public void Configure(EntityTypeBuilder<PayrollAdjustment> builder)
    {
        builder.Property(a => a.Days).HasPrecision(5, 1);
        builder.Property(a => a.Reason).HasMaxLength(500);
    }
}

public class PayrollRunConfiguration : IEntityTypeConfiguration<PayrollRun>
{
    public void Configure(EntityTypeBuilder<PayrollRun> builder)
    {
        builder.Property(r => r.ApprovedBy).HasMaxLength(450);
        builder.HasIndex(r => new { r.Year, r.Month }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class PayrollItemConfiguration : IEntityTypeConfiguration<PayrollItem>
{
    public void Configure(EntityTypeBuilder<PayrollItem> builder)
    {
        builder.HasIndex(i => new { i.PayrollRunId, i.EmployeeId }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasOne(i => i.PayrollRun).WithMany(r => r.Items).HasForeignKey(i => i.PayrollRunId);
        builder.Property(i => i.AbsentDays).HasPrecision(5, 1);
    }
}

public class TaxBracketConfiguration : IEntityTypeConfiguration<TaxBracket>
{
    public void Configure(EntityTypeBuilder<TaxBracket> builder)
    {
        builder.Property(t => t.Rate).HasPrecision(5, 2);
    }
}

public class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.Property(s => s.Key).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Value).HasMaxLength(500).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(250);
        builder.HasIndex(s => s.Key).IsUnique();
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.Property(a => a.UserId).HasMaxLength(450);
        builder.Property(a => a.Action).HasMaxLength(20).IsRequired();
        builder.Property(a => a.EntityName).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityId).HasMaxLength(50).IsRequired();
        builder.HasIndex(a => new { a.EntityName, a.EntityId });
        // The audit screen lists newest first
        builder.HasIndex(a => a.Timestamp);
    }
}
