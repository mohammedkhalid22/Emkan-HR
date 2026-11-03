using HRSystem.Domain.Common;
using HRSystem.Domain.Enums;

namespace HRSystem.Domain.Entities;

// Someone who applied for a job: their CV, what the AI made of it, and where they are in hiring
public class Candidate : BaseEntity
{
    public int JobTitleId { get; set; }
    public JobTitle JobTitle { get; set; } = null!;

    public string CvPath { get; set; } = null!;
    public CandidateStatus Status { get; set; } = CandidateStatus.New;
    public string? Notes { get; set; }

    // Read from the CV (may be empty until analysed)
    public string? FullNameAr { get; set; }
    public string? FullNameEn { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public DateOnly? BirthDate { get; set; }
    public Gender? Gender { get; set; }
    public MaritalStatus? MaritalStatus { get; set; }

    // AI analysis against the job requirements; null until analysed
    public int? MatchPercent { get; set; }
    public string? Summary { get; set; }
    public decimal? ExperienceYears { get; set; }
    public string? Education { get; set; }
    public string? MatchReason { get; set; }
    public string? Skills { get; set; }
    public string? Strengths { get; set; }
    public string? Gaps { get; set; }
    public bool HadRequirements { get; set; }
    public string? Model { get; set; }
    public DateTime? AnalyzedAt { get; set; }

    // Why the last analysis failed (busy service, unreadable file...)
    public string? AnalysisError { get; set; }

    public int? HiredEmployeeId { get; set; }
    public Employee? HiredEmployee { get; set; }
}
