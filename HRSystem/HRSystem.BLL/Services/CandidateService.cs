using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.DAL.Repositories;
using HRSystem.Domain.Entities;
using HRSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.BLL.Services;

public interface ICandidateService
{
    bool IsAvailable { get; }
    Task<List<CandidateListItemDto>> GetListAsync(CandidateFilterDto filter);
    Task<CandidateDetailsDto?> GetDetailsAsync(int id);

    // Saves the CVs only; each one is analysed by its own request so a big batch never times out
    Task<ServiceResult<CandidateUploadResult>> UploadAsync(int jobTitleId, IReadOnlyList<FileUpload> files);
    Task<ServiceResult> AnalyzeAsync(int id);

    Task<ServiceResult> SetStatusAsync(CandidateStatusDto dto);
    Task<ServiceResult> DeleteAsync(int id);
    Task<DocumentFile?> OpenCvAsync(int id);

    Task<CandidateHireDto?> GetForHireAsync(int id);

    // After the employee is saved: their CV and analysis move to the employee profile
    Task CompleteHireAsync(int candidateId, int employeeId);
}

public class CandidateService : ICandidateService
{
    public const int MaxFilesPerUpload = 20;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericRepository<Candidate> _candidates;
    private readonly IFileStorage _fileStorage;
    private readonly IAiClient _ai;

    public CandidateService(IUnitOfWork unitOfWork, IFileStorage fileStorage, IAiClient ai)
    {
        _unitOfWork = unitOfWork;
        _candidates = unitOfWork.Repository<Candidate>();
        _fileStorage = fileStorage;
        _ai = ai;
    }

    public bool IsAvailable => _ai.IsConfigured;

    // Best match first; not-yet-analysed CVs at the end
    public async Task<List<CandidateListItemDto>> GetListAsync(CandidateFilterDto filter)
    {
        var query = _candidates.QueryNoTracking();
        if (filter.JobTitleId is int jobTitleId)
            query = query.Where(c => c.JobTitleId == jobTitleId);
        if (filter.Status is CandidateStatus status)
            query = query.Where(c => c.Status == status);

        var items = await query
            .OrderByDescending(c => c.MatchPercent.HasValue)
            .ThenByDescending(c => c.MatchPercent)
            .ThenByDescending(c => c.CreatedAt)
            .Select(c => new CandidateListItemDto
            {
                Id = c.Id,
                Name = c.FullNameAr ?? c.FullNameEn ?? c.CvPath,
                JobTitleName = c.JobTitle.Name,
                Phone = c.Phone,
                Email = c.Email,
                MatchPercent = c.MatchPercent,
                ExperienceYears = c.ExperienceYears,
                Summary = c.Summary,
                Status = c.Status,
                IsAnalyzed = c.AnalyzedAt != null,
                AnalysisError = c.AnalysisError,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();

        // Until the AI reads the name, the stored path stands in: show the original file name
        foreach (var item in items.Where(i => i.Name.Contains('/')))
            item.Name = EmployeeDocumentService.OriginalFileName(item.Name);
        return items;
    }

    public async Task<CandidateDetailsDto?> GetDetailsAsync(int id)
    {
        var c = await _candidates.QueryNoTracking().Include(x => x.JobTitle).FirstOrDefaultAsync(x => x.Id == id);
        if (c is null)
            return null;

        var fileName = EmployeeDocumentService.OriginalFileName(c.CvPath);
        return new CandidateDetailsDto
        {
            Id = c.Id,
            Name = c.FullNameAr ?? c.FullNameEn ?? fileName,
            CvFileName = fileName,
            JobTitleId = c.JobTitleId,
            JobTitleName = c.JobTitle.Name,
            Status = c.Status,
            Notes = c.Notes,
            Email = c.Email,
            Phone = c.Phone,
            Address = c.Address,
            BirthDate = c.BirthDate,
            HiredEmployeeId = c.HiredEmployeeId,
            AnalysisError = c.AnalysisError,
            CreatedAt = c.CreatedAt,
            Analysis = c.AnalyzedAt is null ? null : new CvAnalysisDto
            {
                JobTitleName = c.JobTitle.Name,
                HadRequirements = c.HadRequirements,
                Summary = c.Summary ?? "",
                ExperienceYears = c.ExperienceYears,
                Education = c.Education,
                MatchPercent = c.MatchPercent ?? 0,
                MatchReason = c.MatchReason,
                Skills = c.Skills,
                Strengths = c.Strengths,
                Gaps = c.Gaps,
                FileName = fileName,
                Model = c.Model ?? "",
                AnalyzedAt = c.AnalyzedAt.Value
            }
        };
    }

    public async Task<ServiceResult<CandidateUploadResult>> UploadAsync(int jobTitleId, IReadOnlyList<FileUpload> files)
    {
        if (!await _unitOfWork.Repository<JobTitle>().QueryNoTracking().AnyAsync(j => j.Id == jobTitleId))
            return ServiceResult<CandidateUploadResult>.Failure("اختار الوظيفة الأول");
        if (files.Count == 0)
            return ServiceResult<CandidateUploadResult>.Failure("اختار ملفات الـ CV");
        if (files.Count > MaxFilesPerUpload)
            return ServiceResult<CandidateUploadResult>.Failure($"أقصى عدد {MaxFilesPerUpload} ملف في المرة");

        var errors = new List<string>();
        var added = 0;
        foreach (var file in files)
        {
            if (FileRules.Validate(file, FileRules.DocumentExtensions) is string error)
            {
                errors.Add($"{file.FileName}: {error}");
                continue;
            }

            var path = await _fileStorage.SaveAsync(file, $"candidates/{jobTitleId}");
            await _candidates.AddAsync(new Candidate { JobTitleId = jobTitleId, CvPath = path });
            added++;
        }

        await _unitOfWork.SaveChangesAsync();
        return ServiceResult<CandidateUploadResult>.Success(new CandidateUploadResult(added, errors));
    }

    public async Task<ServiceResult> AnalyzeAsync(int id)
    {
        if (!_ai.IsConfigured)
            return ServiceResult.Failure(CvAnalysisService.NotConfigured);

        var candidate = await _candidates.Query().Include(c => c.JobTitle).FirstOrDefaultAsync(c => c.Id == id);
        if (candidate is null)
            return ServiceResult.Failure("المتقدم مش موجود");

        var file = await CvAnalysisService.ReadStoredAsync(_fileStorage, candidate.CvPath);
        if (file is null)
            return await FailAsync(candidate, "ملف الـ CV مش موجود على السيرفر");

        var reply = await _ai.GenerateJsonAsync(CvPrompts.Screening,
            CvPrompts.JobPrompt(candidate.JobTitle.Name, candidate.JobTitle.Requirements), file);
        if (!reply.Succeeded)
            return await FailAsync(candidate, reply.Error!);

        var analysis = CvParser.ParseAnalysis(reply.Value!);
        var profile = CvParser.ParseProfile(reply.Value!);
        if (analysis is null || profile is null)
            return await FailAsync(candidate, "الذكاء الاصطناعي رجّع رد مش مفهوم. جرّب تاني");

        candidate.FullNameAr = profile.FullNameAr;
        candidate.FullNameEn = profile.FullNameEn;
        candidate.Email = profile.Email;
        candidate.Phone = profile.Phone;
        candidate.Address = profile.Address;
        candidate.BirthDate = profile.BirthDate;
        candidate.Gender = profile.Gender;
        candidate.MaritalStatus = profile.MaritalStatus;

        candidate.MatchPercent = analysis.MatchPercent;
        candidate.Summary = analysis.Summary;
        candidate.ExperienceYears = analysis.ExperienceYears;
        candidate.Education = analysis.Education;
        candidate.MatchReason = analysis.MatchReason;
        candidate.Skills = CvAnalysisService.Lines(analysis.Skills);
        candidate.Strengths = CvAnalysisService.Lines(analysis.Strengths);
        candidate.Gaps = CvAnalysisService.Lines(analysis.Gaps);
        candidate.HadRequirements = !string.IsNullOrWhiteSpace(candidate.JobTitle.Requirements);
        candidate.Model = _ai.ModelName;
        candidate.AnalyzedAt = DateTime.UtcNow;
        candidate.AnalysisError = null;

        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> SetStatusAsync(CandidateStatusDto dto)
    {
        var candidate = await _candidates.GetByIdAsync(dto.Id);
        if (candidate is null)
            return ServiceResult.Failure("المتقدم مش موجود");
        if (candidate.Status == CandidateStatus.Hired)
            return ServiceResult.Failure("المتقدم ده اتعيّن خلاص");
        if (dto.Status == CandidateStatus.Hired)
            return ServiceResult.Failure("استخدم زرار \"عيّنه كموظف\"");

        candidate.Status = dto.Status;
        candidate.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var candidate = await _candidates.GetByIdAsync(id);
        if (candidate is null)
            return ServiceResult.Failure("المتقدم مش موجود");
        if (candidate.Status == CandidateStatus.Hired)
            return ServiceResult.Failure("مينفعش تمسح متقدم اتعيّن");

        _candidates.Remove(candidate);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<DocumentFile?> OpenCvAsync(int id)
    {
        var path = await _candidates.QueryNoTracking().Where(c => c.Id == id).Select(c => c.CvPath).FirstOrDefaultAsync();
        var stream = path is null ? null : _fileStorage.Open(path);
        return stream is null
            ? null
            : new DocumentFile(stream, EmployeeDocumentService.OriginalFileName(path!), FileRules.ContentTypeFor(path!));
    }

    public Task<CandidateHireDto?> GetForHireAsync(int id)
        => _candidates.QueryNoTracking()
            .Where(c => c.Id == id && c.Status != CandidateStatus.Hired)
            .Select(c => new CandidateHireDto
            {
                CandidateId = c.Id,
                JobTitleId = c.JobTitleId,
                FullNameAr = c.FullNameAr,
                FullNameEn = c.FullNameEn,
                Email = c.Email,
                Phone = c.Phone,
                Address = c.Address,
                BirthDate = c.BirthDate,
                Gender = c.Gender,
                MaritalStatus = c.MaritalStatus
            })
            .FirstOrDefaultAsync();

    public async Task CompleteHireAsync(int candidateId, int employeeId)
    {
        var candidate = await _candidates.Query().Include(c => c.JobTitle).FirstOrDefaultAsync(c => c.Id == candidateId);
        if (candidate is null || candidate.Status == CandidateStatus.Hired)
            return;

        // The CV is copied (not moved) so the recruitment record keeps its own file
        await using var stream = _fileStorage.Open(candidate.CvPath);
        EmployeeDocument? document = null;
        if (stream is not null)
        {
            var name = EmployeeDocumentService.OriginalFileName(candidate.CvPath);
            var path = await _fileStorage.SaveAsync(new FileUpload(stream, name, stream.Length), $"employees/{employeeId}/documents");
            document = new EmployeeDocument { EmployeeId = employeeId, DocumentType = EmployeeDocumentService.CvDocumentType, FilePath = path };
            await _unitOfWork.Repository<EmployeeDocument>().AddAsync(document);
        }

        if (candidate.AnalyzedAt is not null)
        {
            await _unitOfWork.Repository<CvAnalysis>().AddAsync(new CvAnalysis
            {
                EmployeeId = employeeId,
                EmployeeDocument = document,
                JobTitleName = candidate.JobTitle.Name,
                HadRequirements = candidate.HadRequirements,
                Summary = candidate.Summary ?? "",
                ExperienceYears = candidate.ExperienceYears,
                Education = candidate.Education,
                MatchPercent = candidate.MatchPercent ?? 0,
                MatchReason = candidate.MatchReason,
                Skills = candidate.Skills,
                Strengths = candidate.Strengths,
                Gaps = candidate.Gaps,
                Model = candidate.Model ?? ""
            });
        }

        candidate.Status = CandidateStatus.Hired;
        candidate.HiredEmployeeId = employeeId;
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<ServiceResult> FailAsync(Candidate candidate, string error)
    {
        candidate.AnalysisError = error.Length > 300 ? error[..300] : error;
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult.Failure(error);
    }
}
