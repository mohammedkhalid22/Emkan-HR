using HRSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRSystem.DAL.Configurations;

public class CandidateConfiguration : IEntityTypeConfiguration<Candidate>
{
    public void Configure(EntityTypeBuilder<Candidate> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint("CK_Candidates_MatchPercent", "[MatchPercent] IS NULL OR [MatchPercent] BETWEEN 0 AND 100"));

        builder.Property(c => c.CvPath).HasMaxLength(300).IsRequired();
        builder.Property(c => c.Notes).HasMaxLength(1000);
        builder.Property(c => c.FullNameAr).HasMaxLength(150);
        builder.Property(c => c.FullNameEn).HasMaxLength(150);
        builder.Property(c => c.Email).HasMaxLength(150);
        builder.Property(c => c.Phone).HasMaxLength(20);
        builder.Property(c => c.Address).HasMaxLength(250);
        builder.Property(c => c.Summary).HasMaxLength(2000);
        builder.Property(c => c.ExperienceYears).HasPrecision(4, 1);
        builder.Property(c => c.Education).HasMaxLength(500);
        builder.Property(c => c.MatchReason).HasMaxLength(1000);
        builder.Property(c => c.Skills).HasMaxLength(2000);
        builder.Property(c => c.Strengths).HasMaxLength(2000);
        builder.Property(c => c.Gaps).HasMaxLength(2000);
        builder.Property(c => c.Model).HasMaxLength(50);
        builder.Property(c => c.AnalysisError).HasMaxLength(300);

        builder.HasIndex(c => new { c.JobTitleId, c.Status });

        builder.HasOne(c => c.JobTitle).WithMany().HasForeignKey(c => c.JobTitleId);
        builder.HasOne(c => c.HiredEmployee).WithMany().HasForeignKey(c => c.HiredEmployeeId);
    }
}
