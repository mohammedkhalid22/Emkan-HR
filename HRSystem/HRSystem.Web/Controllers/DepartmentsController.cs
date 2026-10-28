using HRSystem.BLL.Services;
using HRSystem.Domain.Identity;
using HRSystem.Web.Extensions;
using HRSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRSystem.Web.Controllers;

public class DepartmentsController : Controller
{
    private readonly IDepartmentService _departmentService;
    private readonly IBranchService _branchService;

    public DepartmentsController(IDepartmentService departmentService, IBranchService branchService)
    {
        _departmentService = departmentService;
        _branchService = branchService;
    }

    public async Task<IActionResult> Index()
        => View(await _departmentService.GetAllAsync());

    [Authorize(Roles = Roles.AdminOrHR)]
    public async Task<IActionResult> Create()
    {
        var model = new DepartmentFormViewModel();
        await FillListsAsync(model);
        return View(model);
    }

    [Authorize(Roles = Roles.AdminOrHR)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DepartmentFormViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _departmentService.CreateAsync(model);
            if (result.Succeeded)
            {
                TempData["Success"] = "القسم اتضاف";
                return RedirectToAction(nameof(Index));
            }
            ModelState.AddServiceError(result);
        }

        await FillListsAsync(model);
        return View(model);
    }

    [Authorize(Roles = Roles.AdminOrHR)]
    public async Task<IActionResult> Edit(int id)
    {
        var dto = await _departmentService.GetForEditAsync(id);
        if (dto is null)
            return NotFound();

        var model = new DepartmentFormViewModel
        {
            Id = dto.Id,
            Name = dto.Name,
            ParentDepartmentId = dto.ParentDepartmentId,
            BranchId = dto.BranchId,
            ManagerId = dto.ManagerId
        };
        await FillListsAsync(model);
        return View(model);
    }

    [Authorize(Roles = Roles.AdminOrHR)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, DepartmentFormViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        if (ModelState.IsValid)
        {
            var result = await _departmentService.UpdateAsync(model);
            if (result.Succeeded)
            {
                TempData["Success"] = "التعديلات اتحفظت";
                return RedirectToAction(nameof(Index));
            }
            ModelState.AddServiceError(result);
        }

        await FillListsAsync(model);
        return View(model);
    }

    [Authorize(Roles = Roles.AdminOrHR)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _departmentService.DeleteAsync(id);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "القسم اتمسح" : result.Error;
        return RedirectToAction(nameof(Index));
    }

    private async Task FillListsAsync(DepartmentFormViewModel model)
    {
        // A department can't be its own parent, so leave it out of the list when editing
        var parents = await _departmentService.GetLookupAsync(model.Id == 0 ? null : model.Id);
        model.ParentDepartments = new SelectList(parents, "Id", "Name", model.ParentDepartmentId);
        model.Branches = new SelectList(await _branchService.GetLookupAsync(), "Id", "Name", model.BranchId);
        model.Managers = new SelectList(await _departmentService.GetManagerCandidatesAsync(), "Id", "Name", model.ManagerId);
    }
}
