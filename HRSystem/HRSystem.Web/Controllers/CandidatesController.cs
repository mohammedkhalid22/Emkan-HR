using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.BLL.Services;
using HRSystem.Domain.Identity;
using HRSystem.Web.Extensions;
using HRSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRSystem.Web.Controllers;

// Recruitment: upload applicants' CVs for a job, let the AI rank them, then hire
[Authorize(Roles = Roles.AdminOrHR)]
public class CandidatesController : Controller
{
    private readonly ICandidateService _candidateService;
    private readonly IJobTitleService _jobTitleService;

    public CandidatesController(ICandidateService candidateService, IJobTitleService jobTitleService)
    {
        _candidateService = candidateService;
        _jobTitleService = jobTitleService;
    }

    public async Task<IActionResult> Index([FromQuery] CandidateFilterDto filter)
    {
        var jobTitles = await _jobTitleService.GetAllAsync();
        return View(new CandidateIndexViewModel
        {
            Filter = filter,
            Items = await _candidateService.GetListAsync(filter),
            JobTitles = new SelectList(jobTitles, "Id", "Name", filter.JobTitleId),
            JobsWithoutRequirements = jobTitles.Where(j => !j.HasRequirements).Select(j => j.Id).ToHashSet(),
            IsAvailable = _candidateService.IsAvailable
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(CandidateService.MaxFilesPerUpload * 5 * 1024 * 1024 + 1024 * 1024)]
    public async Task<IActionResult> Upload(int jobTitleId, List<IFormFile> files)
    {
        var uploads = files.Select(f => f.ToFileUpload()).OfType<FileUpload>().ToList();
        var result = await _candidateService.UploadAsync(jobTitleId, uploads);

        if (!result.Succeeded)
            TempData["Error"] = result.Error;
        else
        {
            var (added, errors) = result.Value!;
            if (added > 0)
                TempData["Success"] = $"اترفع {added} CV. التحليل شغال دلوقتي…";
            if (errors.Count > 0)
                TempData["Error"] = string.Join(" · ", errors);
        }
        return RedirectToAction(nameof(Index), new { jobTitleId });
    }

    // Called by the list page for each CV that still needs analysing
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Analyze(int id)
    {
        var result = await _candidateService.AnalyzeAsync(id);
        return Json(new { ok = result.Succeeded, error = result.Error });
    }

    public async Task<IActionResult> Details(int id)
    {
        var candidate = await _candidateService.GetDetailsAsync(id);
        return candidate is null ? NotFound() : View(candidate);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reanalyze(int id)
    {
        var result = await _candidateService.AnalyzeAsync(id);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "الـ CV اتحلل" : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(CandidateStatusDto dto)
    {
        var result = await _candidateService.SetStatusAsync(dto);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "اتحفظ" : result.Error;
        return RedirectToAction(nameof(Details), new { id = dto.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _candidateService.DeleteAsync(id);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "المتقدم اتمسح" : result.Error;
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Cv(int id)
    {
        var file = await _candidateService.OpenCvAsync(id);
        return file is null ? NotFound() : File(file.Content, file.ContentType, file.FileName);
    }
}
