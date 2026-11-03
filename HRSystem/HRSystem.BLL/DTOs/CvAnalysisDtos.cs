using HRSystem.BLL.Common;

namespace HRSystem.BLL.DTOs;

public class CvAnalysisDto
{
    public string JobTitleName { get; set; } = null!;
    public bool HadRequirements { get; set; }
    public string Summary { get; set; } = null!;
    public decimal? ExperienceYears { get; set; }
    public string? Education { get; set; }
    public int MatchPercent { get; set; }
    public string? MatchReason { get; set; }

    // One item per line
    public string? Skills { get; set; }
    public string? Strengths { get; set; }
    public string? Gaps { get; set; }

    public string? FileName { get; set; }
    public string Model { get; set; } = null!;
    public DateTime AnalyzedAt { get; set; }

    public static IEnumerable<string> Items(string? lines)
        => lines?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
}

public class CvAnalysisPanelDto
{
    public CvAnalysisDto? Latest { get; set; }

    // The employee's uploaded documents, CVs first
    public List<LookupDto> Documents { get; set; } = [];

    public int JobTitleId { get; set; }
    public bool JobHasRequirements { get; set; }
    public bool IsAvailable { get; set; }
}
