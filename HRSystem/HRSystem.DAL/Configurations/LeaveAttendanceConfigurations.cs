using HRSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRSystem.DAL.Configurations;

public class LeaveTypeConfiguration : IEntityTypeConfiguration<LeaveType>
{
    public void Configure(EntityTypeBuilder<LeaveType> builder)
    {
        builder.Property(l => l.Name).HasMaxLength(50).IsRequired();
        builder.HasIndex(l => l.Name).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class LeaveBalanceConfiguration : IEntityTypeConfiguration<LeaveBalance>
{
    public void Configure(EntityTypeBuilder<LeaveBalance> builder)
    {
        builder.Property(b => b.Entitled).HasPrecision(5, 1);
        builder.Property(b => b.Used).HasPrecision(5, 1);
        builder.Ignore(b => b.Remaining);

        builder.HasIndex(b => new { b.EmployeeId, b.LeaveTypeId, b.Year }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasOne(b => b.Employee).WithMany(e => e.LeaveBalances).HasForeignKey(b => b.EmployeeId);
        builder.ToTable(t => t.HasCheckConstraint("CK_LeaveBalances_Used", "[Used] <= [Entitled]"));
    }
}

public class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.Property(r => r.Days).HasPrecision(5, 1);
        builder.Property(r => r.Reason).HasMaxLength(500);
        builder.Property(r => r.RejectionReason).HasMaxLength(500);
        builder.Property(r => r.AttachmentPath).HasMaxLength(300);
        builder.Property(r => r.ManagerActionBy).HasMaxLength(450);
        builder.Property(r => r.HrActionBy).HasMaxLength(450);

        builder.HasIndex(r => r.Status);
        builder.HasOne(r => r.Employee).WithMany(e => e.LeaveRequests).HasForeignKey(r => r.EmployeeId);
        builder.ToTable(t => t.HasCheckConstraint("CK_LeaveRequests_Dates", "[StartDate] <= [EndDate]"));
    }
}

public class PublicHolidayConfiguration : IEntityTypeConfiguration<PublicHoliday>
{
    public void Configure(EntityTypeBuilder<PublicHoliday> builder)
    {
        builder.Property(h => h.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(h => h.Date).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.Property(s => s.Name).HasMaxLength(50).IsRequired();
        builder.HasIndex(s => s.Name).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.ToTable(t => t.HasCheckConstraint("CK_Shifts_Times", "[StartTime] < [EndTime]"));
    }
}

public class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.Property(a => a.Notes).HasMaxLength(250);
        builder.HasIndex(a => new { a.EmployeeId, a.Date }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(a => a.Date);
        builder.HasOne(a => a.Employee).WithMany(e => e.AttendanceRecords).HasForeignKey(a => a.EmployeeId);
    }
}

public class AttendanceRequestConfiguration : IEntityTypeConfiguration<AttendanceRequest>
{
    public void Configure(EntityTypeBuilder<AttendanceRequest> builder)
    {
        builder.Property(r => r.Reason).HasMaxLength(500);
        builder.Property(r => r.RejectionReason).HasMaxLength(500);
        builder.HasIndex(r => new { r.EmployeeId, r.Date });
        builder.ToTable(t => t.HasCheckConstraint("CK_AttendanceRequests_Times", "[FromTime] < [ToTime]"));
    }
}
