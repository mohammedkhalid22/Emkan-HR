using HRSystem.Domain.Identity;
using HRSystem.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace HRSystem.Web.Controllers;

// Database backups: status, history, take one now, download
[Authorize(Roles = Roles.Admin)]
public class BackupController : Controller
{
    private readonly DatabaseBackup _backup;

    public BackupController(DatabaseBackup backup)
    {
        _backup = backup;
    }

    public async Task<IActionResult> Index()
    {
        var model = new BackupViewModel { Options = _backup.Options, State = _backup.State, DatabaseName = _backup.DatabaseName };
        try
        {
            model.History = await _backup.HistoryAsync();
        }
        catch (SqlException)
        {
            // Shared hosting hides msdb
            model.HistoryUnavailable = true;
        }
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Run()
    {
        var result = await _backup.RunAsync(HttpContext.RequestAborted);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
            ? $"النسخة الاحتياطية اتاخدت: {Path.GetFileName(result.File)}"
            : result.Error;
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Download(string name)
    {
        var path = await _backup.FindFileAsync(name);
        if (path is null)
            return NotFound();
        try
        {
            // Opened here, not by the result, so a permissions problem lands in the catch
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            return File(stream, "application/octet-stream", Path.GetFileName(path));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            TempData["Error"] = "البرنامج مالوش صلاحية يقرا الملف ده. انسخه من الفولدر على السيرفر مباشرة.";
            return RedirectToAction(nameof(Index));
        }
    }
}

public class BackupViewModel
{
    public BackupOptions Options { get; set; } = null!;
    public BackupState State { get; set; } = null!;
    public string DatabaseName { get; set; } = null!;
    public List<BackupFile> History { get; set; } = [];
    public bool HistoryUnavailable { get; set; }
}
