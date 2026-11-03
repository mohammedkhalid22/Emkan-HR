using System.ComponentModel.DataAnnotations;
using HRSystem.Domain.Enums;

namespace HRSystem.BLL.DTOs;

public class CandidateFilterDto
{
    public int? JobTitleId { get; set; }
    public CandidateStatus? Status { get; set; }
}

public class CandidateListItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string JobTitleName { get; set; } = null!;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public int? MatchPercent { get; set; }
    public decimal? ExperienceYears { get; set; }
    public string? Summary { get; set; }
    public CandidateStatus Status { get; set; }
    public bool IsAnalyzed { get; set; }
    public string? AnalysisError { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CandidateDetailsDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string CvFileName { get; set; } = null!;
    public int JobTitleId { get; set; }
    public string JobTitleName { get; set; } = null!;
    public CandidateStatus Status { get; set; }
    public string? Notes { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public DateOnly? BirthDate { get; set; }
    public int? HiredEmployeeId { get; set; }
    public string? AnalysisError { get; set; }
    public DateTime CreatedAt { get; set; }

    // Same shape as the employee CV panel, so the result card is shared
    public CvAnalysisDto? Analysis { get; set; }
}

public class CandidateStatusDto
{
    public int Id { get; set; }

    [Display(Name = "الحالة")]
    public CandidateStatus Status { get; set; }

    [Display(Name = "ملاحظات")]
    [StringLength(1000, ErrorMessage = "{0} لازم تكون {1} حرف أو أقل")]
    public string? Notes { get; set; }
}

// What the new employee form is filled with when a candidate is hired
public class CandidateHireDto
{
    public int CandidateId { get; set; }
    public int JobTitleId { get; set; }
    public string? FullNameAr { get; set; }
    public string? FullNameEn { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public DateOnly? BirthDate { get; set; }
    public Gender? Gender { get; set; }
    public MaritalStatus? MaritalStatus { get; set; }
}

public record CandidateUploadResult(int Added, List<string> Errors);
