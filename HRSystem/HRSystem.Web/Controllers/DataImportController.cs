using System.Text;
using System.Text.Json;
using HRSystem.BLL.Common;
using HRSystem.Domain.Identity;
using HRSystem.Web.Extensions;
using HRSystem.Web.Services.DataImport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRSystem.Web.Controllers;

// Basic data from Excel: the template, or any workbook mapped by the AI → preview and pick rows → save → undo
[Authorize(Roles = Roles.AdminOrHR)]
public class DataImportController : Controller
{
    private const string Folder = "imports/data";
    private const string ExcelType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly DataImporter _importer;
    private readonly IFileStorage _files;
    private readonly IAiClient _ai;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<DataImportController> _logger;

    public DataImportController(DataImporter importer, IFileStorage files, IAiClient ai, IWebHostEnvironment environment, ILogger<DataImportController> logger)
    {
        _importer = importer;
        _files = files;
        _ai = ai;
        _environment = environment;
        _logger = logger;
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

        RemoveAbandoned();

        // Kept until saving, so every step reads the very same file
        var token = await _files.SaveAsync(file.ToFileUpload()!, Folder);
        var parsed = Read(token, ImportWorkbook.Read);
        if (parsed is null)
        {
            TempData["Error"] = "الملف مش مقروء. اتأكد إنه Excel سليم";
            return RedirectToAction(nameof(Index));
        }

        // The template: every sheet with data is one of ours, with its required columns. Straight to the preview.
        // (A client file that merely has a sheet called "الفروع" is not the template.)
        var raw = Read(token, ImportMapper.ReadRaw);
        if (parsed.Sheets.Count > 0 && !parsed.Warnings.Any(w => w.Contains("ناقصة"))
            && raw is not null && raw.All(s => ImportSheets.All.Any(t => t.Name.Trim() == s.Name.Trim())))
            return await PreviewAsync(token, null, file.FileName, parsed);

        // Any other workbook: guess which sheet and column is which, then let the AI improve it
        if (raw is null || raw.Count == 0)
        {
            _files.Delete(token);
            TempData["Error"] = "مش لاقي في الملف جداول فيها عناوين أعمدة";
            return RedirectToAction(nameof(Index));
        }
        var mapping = await ImportMapper.AskAiAsync(raw, ImportMapper.Guess(raw), _ai, _logger, HttpContext.RequestAborted);
        return View("Mapping", new DataImportMappingModel(token, file.FileName, raw, mapping));
    }

    // The reviewed mapping: turn the workbook into import rows and preview them
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Map(MappingForm form)
    {
        var raw = form.Token.StartsWith(Folder + "/") ? Read(form.Token, ImportMapper.ReadRaw) : null;
        if (raw is null)
        {
            TempData["Error"] = "الملف مش موجود. ارفعه تاني";
            return RedirectToAction(nameof(Index));
        }

        var mapping = form.ToMapping();
        var json = JsonSerializer.SerializeToUtf8Bytes(mapping);
        var mapToken = await _files.SaveAsync(new FileUpload(new MemoryStream(json), "mapping.json", json.Length), Folder);
        var parsed = ImportMapper.Convert(raw, mapping, await _importer.ReferencesAsync());
        return await PreviewAsync(form.Token, mapToken, form.FileName, parsed);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(string token, string? mapToken, string fileName, List<string> rows)
    {
        var parsed = await LoadAsync(token, mapToken);
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
        if (mapToken is not null)
            _files.Delete(mapToken);
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

    private async Task<IActionResult> PreviewAsync(string token, string? mapToken, string fileName, ImportFile parsed)
    {
        var report = await _importer.RunAsync(parsed, fileName, selected: null, apply: false);
        return View("Preview", new DataImportPreviewModel(token, mapToken, fileName, parsed, report));
    }

    // The import rows again, from the template or from the workbook and its saved mapping
    private async Task<ImportFile?> LoadAsync(string token, string? mapToken)
    {
        if (!token.StartsWith(Folder + "/"))
            return null;
        if (mapToken is null)
            return Read(token, ImportWorkbook.Read);
        if (!mapToken.StartsWith(Folder + "/"))
            return null;

        var raw = Read(token, ImportMapper.ReadRaw);
        var mapping = Read(mapToken, s => JsonSerializer.Deserialize<ImportMapping>(s));
        return raw is null || mapping is null ? null : ImportMapper.Convert(raw, mapping, await _importer.ReferencesAsync());
    }

    // Files uploaded but never saved (the preview was left) go after a day
    private void RemoveAbandoned()
    {
        var folder = Path.Combine(_environment.ContentRootPath, "App_Data", "uploads", Folder.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(folder))
            return;
        foreach (var old in new DirectoryInfo(folder).GetFiles().Where(f => f.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-1)))
        {
            try { old.Delete(); } catch (IOException) { /* next time */ }
        }
    }

    private T? Read<T>(string token, Func<Stream, T> read) where T : class
    {
        try
        {
            using var stream = _files.Open(token);
            return stream is null ? null : read(stream);
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or ArgumentException or JsonException)
        {
            return null;
        }
    }
}

public record DataImportPreviewModel(string Token, string? MapToken, string FileName, ImportFile File, ImportReport Report);

public record DataImportMappingModel(string Token, string FileName, List<RawSheet> Raw, ImportMapping Mapping);

// The mapping screen as posted
public class MappingForm
{
    public string Token { get; set; } = "";
    public string FileName { get; set; } = "";
    public bool CreateMissing { get; set; }
    public List<SheetForm> Sheets { get; set; } = [];

    public class SheetForm
    {
        public string Source { get; set; } = "";
        public string? Target { get; set; }
        public List<ColumnForm> Columns { get; set; } = [];
        public List<ValueForm> Values { get; set; } = [];
    }

    public class ColumnForm
    {
        public string Header { get; set; } = "";
        public string? Target { get; set; }
    }

    public class ValueForm
    {
        public string Column { get; set; } = "";
        public string Source { get; set; } = "";
        public string? Target { get; set; }
    }

    public ImportMapping ToMapping() => new()
    {
        CreateMissing = CreateMissing,
        Sheets = Sheets.Select(s =>
        {
            var target = string.IsNullOrEmpty(s.Target) ? null : ImportSheets.Find(s.Target);
            var map = new SheetMapping { Source = s.Source, Target = target?.Key };
            if (target is null)
                return map;
            foreach (var c in s.Columns.Where(c => !string.IsNullOrEmpty(c.Target) && target.Columns.Any(t => t.Key == c.Target)))
                if (!map.Columns.ContainsValue(c.Target!))
                    map.Columns[c.Header] = c.Target!;
            foreach (var v in s.Values.Where(v => !string.IsNullOrEmpty(v.Target)))
                (map.Values.TryGetValue(v.Column, out var d) ? d : map.Values[v.Column] = [])[v.Source] = v.Target!;
            return map;
        }).ToList()
    };
}
