using HRSystem.BLL.DTOs;
using HRSystem.BLL.Services;
using HRSystem.Domain.Identity;
using HRSystem.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRSystem.Web.Controllers;

public class EmployeeDocumentsController : Controller
{
    private readonly IEmployeeDocumentService _documentService;
    private readonly IEmployeeService _employeeService;

    public EmployeeDocumentsController(IEmployeeDocumentService documentService, IEmployeeService employeeService)
    {
        _documentService = documentService;
        _employeeService = employeeService;
    }

    [Authorize(Roles = Roles.AdminOrHR)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload([Bind(Prefix = "NewDocument")] UploadDocumentDto dto, IFormFile? file)
    {
        var upload = file.ToFileUpload();
        if (!ModelState.IsValid || upload is null)
        {
            TempData["Error"] = upload is null ? "اختار ملف الأول" : "اختار نوع المستند";
        }
        else
        {
            var result = await _documentService.UploadAsync(dto, upload);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "المستند اترفع" : result.Error;
        }

        return RedirectToAction("Details", "Employees", new { id = dto.EmployeeId }, "documents");
    }

    public async Task<IActionResult> Download(int id)
    {
        var scope = await _employeeService.GetScopeAsync(User);
        var file = await _documentService.OpenAsync(id, scope);
        return file is null ? NotFound() : File(file.Content, file.ContentType, file.FileName);
    }

    [Authorize(Roles = Roles.AdminOrHR)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _documentService.DeleteAsync(id);
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction("Index", "Employees");
        }

        TempData["Success"] = "المستند اتمسح";
        return RedirectToAction("Details", "Employees", new { id = result.Value }, "documents");
    }
}
