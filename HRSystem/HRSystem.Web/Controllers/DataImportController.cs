using HRSystem.BLL.Common;
using HRSystem.Domain.Identity;
using HRSystem.Web.Extensions;
using HRSystem.Web.Services.DataImport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRSystem.Web.Controllers;

// Basic data from one Excel workbook: template → upload → preview and pick rows → save → undo
[Authorize(Roles = Roles.AdminOrHR)]
public class DataImportController : Controller
{
    private const string Folder = "imports/data";
    private const string ExcelType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly DataImporter _importer;
    private readonly IFileStorage _files;

    public DataImportController(DataImporter importer, IFileStorage files)
    {
        _importer = importer;
        _files = files;
    }

    public async Task<IActionResult> Index() => View(await _importer.HistoryAsync());

    public async Task<IActionResult> Template()
        => File(ImportWorkbook.Template(await _importer.ReferencesAsync()), ExcelType, "Emkan-HR-basic-data.xlsx");

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile? file)
    {
        if (file is null || file.Length == 0 || !file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "اختار ملف Excel (.xlsx)";
            return RedirectToAction(nameof(Index));
        }

        // Kept until saving, so the preview and the save read the very same file
        var token = await _files.SaveAsync(file.ToFileUpload()!, Folder);
        var parsed = ReadFile(token);
        if (parsed is null)
        {
            TempData["Error"] = "الملف مش مقروء. اتأكد إنه Excel سليم";
            return RedirectToAction(nameof(Index));
        }

        var report = await _importer.RunAsync(parsed, file.FileName, selected: null, apply: false);
        return View("Preview", new DataImportPreviewModel(token, file.FileName, parsed, report));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(string token, string fileName, List<string> rows)
    {
        var parsed = token.StartsWith(Folder + "/") ? ReadFile(token) : null;
        if (parsed is null)
        {
            TempData["Error"] = "الملف مش موجود. ارفعه تاني";
            return RedirectToAction(nameof(Index));
        }
        if (rows.Count == 0)
        {
            TempData["Error"] = "مفيش صفوف متختارة";
            return RedirectToAction(nameof(Index));
        }

        var report = await _importer.RunAsync(parsed, fileName, rows.ToHashSet(), apply: true);
        _files.Delete(token);
        return View("Result", report);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Undo(int id)
    {
        var (removed, kept) = await _importer.UndoAsync(id);
        TempData["Success"] = $"اتشال {removed} سجل من اللي الاستيراد ده عمله";
        if (kept.Count > 0)
            TempData["Error"] = "فضلوا لأنهم اتستخدموا بعد الاستيراد: " + string.Join(" · ", kept.Take(10));
        return RedirectToAction(nameof(Index));
    }

    private ImportFile? ReadFile(string token)
    {
        try
        {
            using var stream = _files.Open(token);
            return stream is null ? null : ImportWorkbook.Read(stream);
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or ArgumentException)
        {
            return null;
        }
    }
}

public record DataImportPreviewModel(string Token, string FileName, ImportFile File, ImportReport Report);
