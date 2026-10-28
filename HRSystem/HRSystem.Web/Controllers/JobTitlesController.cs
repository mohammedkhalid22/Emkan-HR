using HRSystem.BLL.DTOs;
using HRSystem.BLL.Services;
using HRSystem.Domain.Identity;
using HRSystem.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRSystem.Web.Controllers;

public class JobTitlesController : Controller
{
    private readonly IJobTitleService _jobTitleService;

    public JobTitlesController(IJobTitleService jobTitleService)
    {
        _jobTitleService = jobTitleService;
    }

    public async Task<IActionResult> Index()
        => View(await _jobTitleService.GetAllAsync());

    [Authorize(Roles = Roles.AdminOrHR)]
    public IActionResult Create() => View(new SaveJobTitleDto());

    [Authorize(Roles = Roles.AdminOrHR)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SaveJobTitleDto dto)
    {
        if (!ModelState.IsValid)
            return View(dto);

        var result = await _jobTitleService.CreateAsync(dto);
        if (!result.Succeeded)
        {
            ModelState.AddServiceError(result);
            return View(dto);
        }

        TempData["Success"] = "المسمى الوظيفي اتضاف";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.AdminOrHR)]
    public async Task<IActionResult> Edit(int id)
    {
        var dto = await _jobTitleService.GetForEditAsync(id);
        return dto is null ? NotFound() : View(dto);
    }

    [Authorize(Roles = Roles.AdminOrHR)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SaveJobTitleDto dto)
    {
        if (id != dto.Id)
            return BadRequest();
        if (!ModelState.IsValid)
            return View(dto);

        var result = await _jobTitleService.UpdateAsync(dto);
        if (!result.Succeeded)
        {
            ModelState.AddServiceError(result);
            return View(dto);
        }

        TempData["Success"] = "التعديلات اتحفظت";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.AdminOrHR)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _jobTitleService.DeleteAsync(id);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "المسمى الوظيفي اتمسح" : result.Error;
        return RedirectToAction(nameof(Index));
    }
}
