using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.DAL.Repositories;
using HRSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.BLL.Services;

public interface ICvAnalysisService
{
    bool IsAvailable { get; }
    Task<CvAnalysisPanelDto> GetPanelAsync(int employeeId);
    Task<ServiceResult> AnalyzeAsync(int employeeId, int documentId);

    // Reads a CV that isn't saved anywhere yet, to pre-fill the new employee form
    Task<ServiceResult<CvProfile>> ReadProfileAsync(FileUpload file);
}

public class CvAnalysisService : ICvAnalysisService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorage _fileStorage;
    private readonly IAiClient _ai;

    public CvAnalysisService(IUnitOfWork unitOfWork, IFileStorage fileStorage, IAiClient ai)
    {
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
        _ai = ai;
    }

    public bool IsAvailable => _ai.IsConfigured;

    public async Task<CvAnalysisPanelDto> GetPanelAsync(int employeeId)
    {
        var latest = await _unitOfWork.Repository<CvAnalysis>().QueryNoTracking()
            .Where(a => a.EmployeeId == employeeId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new CvAnalysisDto
            {
                JobTitleName = a.JobTitleName,
                HadRequirements = a.HadRequirements,
                Summary = a.Summary,
                ExperienceYears = a.ExperienceYears,
                Education = a.Education,
                MatchPercent = a.MatchPercent,
                MatchReason = a.MatchReason,
                Skills = a.Skills,
                Strengths = a.Strengths,
                Gaps = a.Gaps,
                FileName = a.EmployeeDocument != null ? a.EmployeeDocument.FilePath : null,
                Model = a.Model,
                AnalyzedAt = a.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (latest?.FileName is string path)
            latest.FileName = EmployeeDocumentService.OriginalFileName(path);

        var documents = await _unitOfWork.Repository<EmployeeDocument>().QueryNoTracking()
            .Where(d => d.EmployeeId == employeeId)
            .OrderByDescending(d => d.DocumentType == EmployeeDocumentService.CvDocumentType)
            .ThenByDescending(d => d.CreatedAt)
            .Select(d => new { d.Id, d.DocumentType, d.FilePath })
            .ToListAsync();

        var jobRequirements = await _unitOfWork.Repository<Employee>().QueryNoTracking()
            .Where(e => e.Id == employeeId)
            .Select(e => new { e.JobTitleId, e.JobTitle.Requirements })
            .FirstAsync();

        return new CvAnalysisPanelDto
        {
            Latest = latest,
            Documents = documents
                .Select(d => new LookupDto(d.Id, $"{d.DocumentType} — {EmployeeDocumentService.OriginalFileName(d.FilePath)}"))
                .ToList(),
            JobTitleId = jobRequirements.JobTitleId,
            JobHasRequirements = !string.IsNullOrWhiteSpace(jobRequirements.Requirements),
            IsAvailable = _ai.IsConfigured
        };
    }

    public async Task<ServiceResult> AnalyzeAsync(int employeeId, int documentId)
    {
        if (!_ai.IsConfigured)
            return ServiceResult.Failure(NotConfigured);

        var employee = await _unitOfWork.Repository<Employee>().QueryNoTracking()
            .Where(e => e.Id == employeeId)
            .Select(e => new { e.JobTitle.Name, e.JobTitle.Requirements })
            .FirstOrDefaultAsync();
        if (employee is null)
            return ServiceResult.Failure("الموظف مش موجود");

        var document = await _unitOfWork.Repository<EmployeeDocument>().QueryNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.EmployeeId == employeeId);
        if (document is null)
            return ServiceResult.Failure("اختار ملف الـ CV من مستندات الموظف");

        var file = await ReadStoredAsync(_fileStorage, document.FilePath);
        if (file is null)
            return ServiceResult.Failure("ملف الـ CV مش موجود على السيرفر. ارفعه تاني");

        var hasRequirements = !string.IsNullOrWhiteSpace(employee.Requirements);
        var reply = await _ai.GenerateJsonAsync(CvPrompts.Analysis, CvPrompts.JobPrompt(employee.Name, employee.Requirements), file);
        if (!reply.Succeeded)
            return reply;

        var result = CvParser.ParseAnalysis(reply.Value!);
        if (result is null)
            return ServiceResult.Failure("الذكاء الاصطناعي رجّع رد مش مفهوم. جرّب تاني");

        await _unitOfWork.Repository<CvAnalysis>().AddAsync(new CvAnalysis
        {
            EmployeeId = employeeId,
            EmployeeDocumentId = document.Id,
            JobTitleName = employee.Name,
            HadRequirements = hasRequirements,
            Summary = result.Summary,
            ExperienceYears = result.ExperienceYears,
            Education = result.Education,
            MatchPercent = result.MatchPercent,
            MatchReason = result.MatchReason,
            Skills = Lines(result.Skills),
            Strengths = Lines(result.Strengths),
            Gaps = Lines(result.Gaps),
            Model = _ai.ModelName
        });
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult<CvProfile>> ReadProfileAsync(FileUpload upload)
    {
        if (!_ai.IsConfigured)
            return ServiceResult<CvProfile>.Failure(NotConfigured);
        if (FileRules.Validate(upload, FileRules.DocumentExtensions) is string error)
            return ServiceResult<CvProfile>.Failure(error);

        using var memory = new MemoryStream();
        await upload.Content.CopyToAsync(memory);
        var file = new AiFile(memory.ToArray(), FileRules.ContentTypeFor(upload.FileName));

        var reply = await _ai.GenerateJsonAsync(CvPrompts.Profile, "The CV is attached.", file);
        if (!reply.Succeeded)
            return ServiceResult<CvProfile>.From(reply);

        return CvParser.ParseProfile(reply.Value!) is { } profile
            ? ServiceResult<CvProfile>.Success(profile)
            : ServiceResult<CvProfile>.Failure("الذكاء الاصطناعي رجّع رد مش مفهوم. جرّب تاني");
    }

    public const string NotConfigured = "خدمة الذكاء الاصطناعي مش متظبطة. حط مفتاح الـ API في إعدادات البرنامج (appsettings) الأول";

    internal static async Task<AiFile?> ReadStoredAsync(IFileStorage storage, string path)
    {
        await using var stream = storage.Open(path);
        if (stream is null)
            return null;

        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        return new AiFile(memory.ToArray(), FileRules.ContentTypeFor(path));
    }

    internal static string? Lines(List<string> items) => items.Count == 0 ? null : string.Join('\n', items);
}
