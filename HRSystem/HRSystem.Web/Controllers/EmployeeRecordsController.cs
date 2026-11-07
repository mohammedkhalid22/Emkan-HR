using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.BLL.Services;
using HRSystem.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRSystem.Web.Controllers;

// Rows on an employee's profile: family, qualifications, past experience
[Authorize(Roles = Roles.AdminOrHR)]
public class EmployeeRecordsController : Controller
{
    private readonly IEmployeeRecordsService _records;

    public EmployeeRecordsController(IEmployeeRecordsService records)
    {
        _records = records;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddFamily([Bind(Prefix = "NewFamilyMember")] FamilyMemberDto dto)
        => Back(dto.EmployeeId, "#family", ModelState.IsValid ? await _records.AddFamilyMemberAsync(dto) : Invalid(), "اتضاف لأفراد الأسرة");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddQualification([Bind(Prefix = "NewQualification")] QualificationDto dto)
        => Back(dto.EmployeeId, "#qualifications", ModelState.IsValid ? await _records.AddQualificationAsync(dto) : Invalid(), "المؤهل اتضاف");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddExperience([Bind(Prefix = "NewExperience")] ExperienceDto dto)
        => Back(dto.EmployeeId, "#qualifications", ModelState.IsValid ? await _records.AddExperienceAsync(dto) : Invalid(), "الخبرة اتضافت");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFamily(int id) => Deleted(await _records.DeleteFamilyMemberAsync(id), "#family");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteQualification(int id) => Deleted(await _records.DeleteQualificationAsync(id), "#qualifications");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteExperience(int id) => Deleted(await _records.DeleteExperienceAsync(id), "#qualifications");

    private ServiceResult Invalid()
        => ServiceResult.Failure(ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault() ?? "البيانات مش صحيحة");

    private IActionResult Back(int employeeId, string tab, ServiceResult result, string success)
    {
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? success : result.Error;
        return Redirect(Url.Action("Details", "Employees", new { id = employeeId }) + tab);
    }

    private IActionResult Deleted(ServiceResult<int> result, string tab)
    {
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction("Index", "Employees");
        }
        TempData["Success"] = "اتمسح";
        return Redirect(Url.Action("Details", "Employees", new { id = result.Value }) + tab);
    }
}
