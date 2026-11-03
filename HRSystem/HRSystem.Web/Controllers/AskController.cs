using HRSystem.BLL.Services;
using HRSystem.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRSystem.Web.Controllers;

// "Ask the system": HR asks a question in Arabic, the AI answers from the HR data
[Authorize(Roles = Roles.AdminOrHR)]
public class AskController : Controller
{
    private readonly IAskService _askService;

    public AskController(IAskService askService)
    {
        _askService = askService;
    }

    public IActionResult Index()
    {
        ViewData["IsAvailable"] = _askService.IsAvailable;
        return View();
    }

    // month = "yyyy-MM"; the reply is JSON for the chat page
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Question(string? question, string? month)
    {
        var today = DateTime.Today;
        var period = DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", out var m) && m.Year is >= 2000 and <= 2100
            ? m
            : new DateOnly(today.Year, today.Month, 1);

        var result = await _askService.AskAsync(question ?? "", period.Year, period.Month);
        return result.Succeeded
            ? Json(new { ok = true, answer = result.Value!.Text, columns = result.Value.Columns, rows = result.Value.Rows })
            : Json(new { ok = false, error = result.Error });
    }
}
