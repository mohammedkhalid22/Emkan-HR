using HRSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRSystem.DAL.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.Property(e => e.Code).HasMaxLength(20).IsRequired();
        builder.Property(e => e.FullNameAr).HasMaxLength(150).IsRequired();
        builder.Property(e => e.FullNameEn).HasMaxLength(150);
        builder.Property(e => e.NationalId).HasColumnType("char(14)").IsRequired();
        builder.Property(e => e.Phone).HasMaxLength(20);
        builder.Property(e => e.Email).HasMaxLength(150);
        builder.Property(e => e.Address).HasMaxLength(250);
        builder.Property(e => e.PhotoPath).HasMaxLength(300);
        builder.Property(e => e.BankAccount).HasMaxLength(34);
        builder.Property(e => e.TerminationReason).HasMaxLength(250);

        builder.HasIndex(e => e.Code).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.NationalId).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.Email).IsUnique().HasFilter("[Email] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(e => e.UserId).IsUnique().HasFilter("[UserId] IS NOT NULL");
        builder.HasIndex(e => new { e.DepartmentId, e.Status });

        builder.HasOne(e => e.Department)
            .WithMany(d => d.Employees)
            .HasForeignKey(e => e.DepartmentId);

        builder.HasOne(e => e.JobTitle)
            .WithMany(j => j.Employees)
            .HasForeignKey(e => e.JobTitleId);

        builder.HasOne(e => e.Manager)
            .WithMany(e => e.Subordinates)
            .HasForeignKey(e => e.ManagerId);

        builder.HasOne(e => e.Shift)
            .WithMany(s => s.Employees)
            .HasForeignKey(e => e.ShiftId);

        builder.HasOne(e => e.Position)
            .WithMany(p => p.Employees)
            .HasForeignKey(e => e.PositionId);

        builder.Property(e => e.Nationality).HasMaxLength(50);
        builder.Property(e => e.BloodType).HasMaxLength(5);
        builder.Property(e => e.InsuranceNumber).HasMaxLength(20);
        builder.Property(e => e.PassportNumber).HasMaxLength(20);
        builder.Property(e => e.DrivingLicenseNumber).HasMaxLength(30);
        builder.Property(e => e.BankName).HasMaxLength(100);
        builder.Property(e => e.BankBranch).HasMaxLength(100);
        builder.Property(e => e.Iban).HasMaxLength(34);
        builder.Property(e => e.Governorate).HasMaxLength(50);
        builder.Property(e => e.City).HasMaxLength(50);
        builder.Property(e => e.EmergencyContactName).HasMaxLength(150);
        builder.Property(e => e.EmergencyContactRelation).HasMaxLength(50);
        builder.Property(e => e.EmergencyContactPhone).HasMaxLength(20);
        builder.Property(e => e.FingerprintId).HasMaxLength(20);
        // One person per number on the device
        builder.HasIndex(e => e.FingerprintId).IsUnique().HasFilter("[FingerprintId] IS NOT NULL AND [IsDeleted] = 0");

        builder.HasOne(e => e.User)
            .WithOne(u => u.Employee)
            .HasForeignKey<Employee>(e => e.UserId);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Employees_BasicSalary", "[BasicSalary] >= 0");
            t.HasCheckConstraint("CK_Employees_InsuranceSalary", "[InsuranceSalary] >= 0");
        });
    }
}

public class EmployeeDocumentConfiguration : IEntityTypeConfiguration<EmployeeDocument>
{
    public void Configure(EntityTypeBuilder<EmployeeDocument> builder)
    {
        builder.Property(d => d.DocumentType).HasMaxLength(50).IsRequired();
        builder.Property(d => d.FilePath).HasMaxLength(300).IsRequired();
    }
}

public class CvAnalysisConfiguration : IEntityTypeConfiguration<CvAnalysis>
{
    public void Configure(EntityTypeBuilder<CvAnalysis> builder)
    {
        builder.ToTable("CvAnalyses", t => t.HasCheckConstraint("CK_CvAnalyses_MatchPercent", "[MatchPercent] BETWEEN 0 AND 100"));
        builder.Property(a => a.JobTitleName).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Summary).HasMaxLength(2000).IsRequired();
        builder.Property(a => a.ExperienceYears).HasPrecision(4, 1);
        builder.Property(a => a.Education).HasMaxLength(500);
        builder.Property(a => a.MatchReason).HasMaxLength(1000);
        builder.Property(a => a.Skills).HasMaxLength(2000);
        builder.Property(a => a.Strengths).HasMaxLength(2000);
        builder.Property(a => a.Gaps).HasMaxLength(2000);
        builder.Property(a => a.Model).HasMaxLength(50).IsRequired();
        builder.HasIndex(a => a.EmployeeId);

        builder.HasOne(a => a.Employee).WithMany().HasForeignKey(a => a.EmployeeId);
        builder.HasOne(a => a.EmployeeDocument).WithMany().HasForeignKey(a => a.EmployeeDocumentId);
    }
}

public class EmployeeFamilyMemberConfiguration : IEntityTypeConfiguration<EmployeeFamilyMember>
{
    public void Configure(EntityTypeBuilder<EmployeeFamilyMember> builder)
    {
        builder.Property(f => f.Name).HasMaxLength(150).IsRequired();
        builder.Property(f => f.NationalId).HasColumnType("char(14)");
        builder.HasOne(f => f.Employee).WithMany(e => e.FamilyMembers).HasForeignKey(f => f.EmployeeId);
    }
}

public class EmployeeQualificationConfiguration : IEntityTypeConfiguration<EmployeeQualification>
{
    public void Configure(EntityTypeBuilder<EmployeeQualification> builder)
    {
        builder.Property(q => q.Title).HasMaxLength(150).IsRequired();
        builder.Property(q => q.Institution).HasMaxLength(150);
        builder.Property(q => q.Grade).HasMaxLength(50);
        builder.HasOne(q => q.Employee).WithMany(e => e.Qualifications).HasForeignKey(q => q.EmployeeId);
    }
}

public class EmployeeExperienceConfiguration : IEntityTypeConfiguration<EmployeeExperience>
{
    public void Configure(EntityTypeBuilder<EmployeeExperience> builder)
    {
        builder.Property(x => x.Company).HasMaxLength(150).IsRequired();
        builder.Property(x => x.JobTitle).HasMaxLength(100);
        builder.Property(x => x.LeavingReason).HasMaxLength(250);
        builder.HasOne(x => x.Employee).WithMany(e => e.Experiences).HasForeignKey(x => x.EmployeeId);
    }
}

public class EmployeeHistoryConfiguration : IEntityTypeConfiguration<EmployeeHistory>
{
    public void Configure(EntityTypeBuilder<EmployeeHistory> builder)
    {
        builder.ToTable("EmployeeHistory");
        builder.Property(h => h.ChangeType).HasMaxLength(50).IsRequired();
        builder.Property(h => h.OldValue).HasMaxLength(200);
        builder.Property(h => h.NewValue).HasMaxLength(200);
    }
}
