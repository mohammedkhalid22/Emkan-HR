using HRSystem.BLL.DTOs;
using HRSystem.BLL.Services;
using HRSystem.Domain.Identity;
using HRSystem.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRSystem.Web.Controllers;

public class BranchesController : Controller
{
    private readonly IBranchService _branchService;

    public BranchesController(IBranchService branchService)
    {
        _branchService = branchService;
    }

    public async Task<IActionResult> Index()
        => View(await _branchService.GetAllAsync());

    [Authorize(Roles = Roles.AdminOrHR)]
    public IActionResult Create() => View(new SaveBranchDto());

    [Authorize(Roles = Roles.AdminOrHR)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SaveBranchDto dto)
    {
        if (!ModelState.IsValid)
            return View(dto);

        var result = await _branchService.CreateAsync(dto);
        if (!result.Succeeded)
        {
            ModelState.AddServiceError(result);
            return View(dto);
        }

        TempData["Success"] = "الفرع اتضاف";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.AdminOrHR)]
    public async Task<IActionResult> Edit(int id)
    {
        var dto = await _branchService.GetForEditAsync(id);
        return dto is null ? NotFound() : View(dto);
    }

    [Authorize(Roles = Roles.AdminOrHR)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SaveBranchDto dto)
    {
        if (id != dto.Id)
            return BadRequest();
        if (!ModelState.IsValid)
            return View(dto);

        var result = await _branchService.UpdateAsync(dto);
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
        var result = await _branchService.DeleteAsync(id);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "الفرع اتمسح" : result.Error;
        return RedirectToAction(nameof(Index));
    }
}
