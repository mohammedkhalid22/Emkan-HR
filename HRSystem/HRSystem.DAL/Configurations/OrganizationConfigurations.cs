using HRSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRSystem.DAL.Configurations;

public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.Property(b => b.Name).HasMaxLength(100).IsRequired();
        builder.Property(b => b.Address).HasMaxLength(250);
        builder.HasIndex(b => b.Name).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.Property(d => d.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(d => d.Name).IsUnique().HasFilter("[IsDeleted] = 0");

        builder.HasOne(d => d.ParentDepartment)
            .WithMany(d => d.SubDepartments)
            .HasForeignKey(d => d.ParentDepartmentId);

        builder.HasOne(d => d.Branch)
            .WithMany(b => b.Departments)
            .HasForeignKey(d => d.BranchId);

        // Department.Manager and Employee.Department form a cycle, so the manager side stays optional
        builder.HasOne(d => d.Manager)
            .WithMany()
            .HasForeignKey(d => d.ManagerId);
    }
}

public class JobTitleConfiguration : IEntityTypeConfiguration<JobTitle>
{
    public void Configure(EntityTypeBuilder<JobTitle> builder)
    {
        builder.Property(j => j.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(j => j.Name).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.ToTable(t => t.HasCheckConstraint("CK_JobTitles_SalaryRange",
            "[MinSalary] IS NULL OR [MaxSalary] IS NULL OR [MinSalary] <= [MaxSalary]"));
    }
}
