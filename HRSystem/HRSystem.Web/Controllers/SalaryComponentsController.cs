using HRSystem.BLL.DTOs;
using HRSystem.BLL.Services;
using HRSystem.Domain.Identity;
using HRSystem.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRSystem.Web.Controllers;

[Authorize(Roles = Roles.AdminOrHR)]
public class SalaryComponentsController : Controller
{
    private readonly ISalaryComponentService _componentService;

    public SalaryComponentsController(ISalaryComponentService componentService)
    {
        _componentService = componentService;
    }

    public async Task<IActionResult> Index() => View(await _componentService.GetAllAsync());

    public IActionResult Create() => View(new SaveSalaryComponentDto());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SaveSalaryComponentDto dto)
    {
        if (!ModelState.IsValid)
            return View(dto);

        var result = await _componentService.CreateAsync(dto);
        if (!result.Succeeded)
        {
            ModelState.AddServiceError(result);
            return View(dto);
        }

        TempData["Success"] = "المكوّن اتضاف";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var dto = await _componentService.GetForEditAsync(id);
        return dto is null ? NotFound() : View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SaveSalaryComponentDto dto)
    {
        if (id != dto.Id)
            return BadRequest();
        if (!ModelState.IsValid)
            return View(dto);

        var result = await _componentService.UpdateAsync(dto);
        if (!result.Succeeded)
        {
            ModelState.AddServiceError(result);
            return View(dto);
        }

        TempData["Success"] = "التعديلات اتحفظت";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _componentService.DeleteAsync(id);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "المكوّن اتمسح" : result.Error;
        return RedirectToAction(nameof(Index));
    }

    // ---------- Per employee (from the employee profile) ----------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetForEmployee(int employeeId, int componentId, decimal value)
    {
        var result = await _componentService.SetForEmployeeAsync(employeeId, componentId, value);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "المكوّن اتحفظ" : result.Error;
        return RedirectToAction("Details", "Employees", new { id = employeeId }, "salary");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveFromEmployee(int id)
    {
        var result = await _componentService.RemoveFromEmployeeAsync(id);
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction("Index", "Employees");
        }

        TempData["Success"] = "المكوّن اتشال";
        return RedirectToAction("Details", "Employees", new { id = result.Value }, "salary");
    }
}
