using HRSystem.BLL.Services;
using HRSystem.Domain.Identity;
using HRSystem.Web.Extensions;
using HRSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRSystem.Web.Controllers;

// Positions (المواقع الوظيفية): the seats of the org tree
[Authorize(Roles = Roles.AdminOrHR)]
public class PositionsController : Controller
{
    private readonly IPositionService _positionService;
    private readonly IDepartmentService _departmentService;
    private readonly IJobTitleService _jobTitleService;

    public PositionsController(IPositionService positionService, IDepartmentService departmentService, IJobTitleService jobTitleService)
    {
        _positionService = positionService;
        _departmentService = departmentService;
        _jobTitleService = jobTitleService;
    }

    public async Task<IActionResult> Index(int? departmentId)
        => View(new PositionsIndexViewModel
        {
            DepartmentId = departmentId,
            Positions = await _positionService.GetAllAsync(departmentId),
            Departments = new SelectList(await _departmentService.GetLookupAsync(), "Id", "Name", departmentId)
        });

    public async Task<IActionResult> Create(int? departmentId, int? parentId)
    {
        var model = new PositionFormViewModel { DepartmentId = departmentId, ParentPositionId = parentId };
        await FillListsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PositionFormViewModel model, string? returnTo)
    {
        if (ModelState.IsValid)
        {
            var result = await _positionService.CreateAsync(model);
            if (result.Succeeded)
            {
                TempData["Success"] = "الموقع الوظيفي اتضاف";
                return returnTo == "tree" ? RedirectToAction("Index", "OrgChart") : RedirectToAction(nameof(Index), new { departmentId = model.DepartmentId });
            }
            ModelState.AddServiceError(result);
        }

        await FillListsAsync(model);
        return View(model);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var dto = await _positionService.GetForEditAsync(id);
        if (dto is null)
            return NotFound();

        var model = new PositionFormViewModel
        {
            Id = dto.Id,
            Name = dto.Name,
            DepartmentId = dto.DepartmentId,
            JobTitleId = dto.JobTitleId,
            ParentPositionId = dto.ParentPositionId,
            Capacity = dto.Capacity
        };
        await FillListsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PositionFormViewModel model, string? returnTo)
    {
        if (id != model.Id)
            return BadRequest();

        if (ModelState.IsValid)
        {
            var result = await _positionService.UpdateAsync(model);
            if (result.Succeeded)
            {
                TempData["Success"] = "التعديلات اتحفظت";
                return returnTo == "tree" ? RedirectToAction("Index", "OrgChart") : RedirectToAction(nameof(Index), new { departmentId = model.DepartmentId });
            }
            ModelState.AddServiceError(result);
        }

        await FillListsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _positionService.DeleteAsync(id);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "الموقع الوظيفي اتمسح" : result.Error;
        return RedirectToAction(nameof(Index));
    }

    private async Task FillListsAsync(PositionFormViewModel model)
    {
        model.Departments = new SelectList(await _departmentService.GetLookupAsync(), "Id", "Name", model.DepartmentId);
        model.JobTitles = new SelectList(await _jobTitleService.GetLookupAsync(), "Id", "Name", model.JobTitleId);
        model.Parents = new SelectList(await _positionService.GetParentLookupAsync(model.Id == 0 ? null : model.Id), "Id", "Name", model.ParentPositionId);
    }
}
