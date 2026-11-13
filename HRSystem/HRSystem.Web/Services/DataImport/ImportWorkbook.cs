using System.Globalization;
using ClosedXML.Excel;

namespace HRSystem.Web.Services.DataImport;

// One data row of a sheet: its Excel row number and the cells by column key (blank cells left out)
public record ImportRow(int RowNumber, Dictionary<string, string> Cells)
{
    public string? Get(string key) => Cells.TryGetValue(key, out var v) ? v : null;
}

public record ImportFile(Dictionary<string, List<ImportRow>> Sheets, List<string> Warnings)
{
    public int RowCount => Sheets.Values.Sum(s => s.Count);
}

// The template people fill in, and reading it back
public static class ImportWorkbook
{
    private static readonly XLColor HeaderFill = XLColor.FromHtml("#1d4f91");
    private static readonly XLColor RequiredFill = XLColor.FromHtml("#b3261e");

    // existing = names already in the system per sheet, offered as the first values of reference dropdowns
    public static byte[] Template(Dictionary<string, List<string>> references)
    {
        using var workbook = new XLWorkbook();
        workbook.RightToLeft = true;

        var guide = workbook.AddWorksheet("تعليمات");
        guide.RightToLeft = true;
        guide.Cell(1, 1).Value = "استيراد البيانات الأساسية — إمكان HR";
        guide.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(HeaderFill);
        string[] notes =
        [
            "كل شيت = شاشة من البرنامج. املا اللي عندك بس، والشيتات الفاضية بتتجاهل.",
            "العناوين اللي باللون الأحمر لازم تتملا. حط الماوس على أي عنوان تشوف ملاحظته.",
            "الربط بين الشيتات بالاسم: الموظف في «الإدارة / القسم» بنفس الاسم المكتوب في شيت الإدارات والأقسام (أو الموجود في البرنامج).",
            "لو السجل موجود في البرنامج بنفس الاسم (أو نفس الرقم القومي للموظف) بيتحدّث، والخانة الفاضية مش بتمسح القيمة القديمة.",
            "التواريخ بالشكل 2026-11-14 أو كتاريخ Excel عادي، والمواعيد زي 09:00.",
            "بعد الرفع هتشوف معاينة لكل شيت، وتختار الصفوف اللي تتحفظ قبل أي حفظ.",
            "متغيّرش أسماء الشيتات ولا العناوين.",
        ];
        for (var i = 0; i < notes.Length; i++)
            guide.Cell(3 + i, 1).Value = $"{i + 1}. {notes[i]}";
        guide.Cell(3 + notes.Length + 1, 1).Value = "الشيتات بترتيب الحفظ:";
        guide.Cell(3 + notes.Length + 1, 1).Style.Font.SetBold();
        for (var i = 0; i < ImportSheets.All.Length; i++)
            guide.Cell(3 + notes.Length + 2 + i, 1).Value = $"• {ImportSheets.All[i].Name}: {ImportSheets.All[i].Description}";
        guide.Column(1).Width = 120;

        // Hidden sheet holding the dropdown lists
        var lists = workbook.AddWorksheet("lists");
        lists.Visibility = XLWorksheetVisibility.VeryHidden;
        var listColumn = 0;

        foreach (var sheet in ImportSheets.All)
        {
            var ws = workbook.AddWorksheet(sheet.Name);
            ws.RightToLeft = true;
            for (var c = 0; c < sheet.Columns.Length; c++)
            {
                var column = sheet.Columns[c];
                var cell = ws.Cell(1, c + 1);
                cell.Value = column.Header;
                cell.Style.Font.SetBold().Font.SetFontColor(XLColor.White)
                    .Fill.SetBackgroundColor(column.Required ? RequiredFill : HeaderFill)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Alignment.SetWrapText();
                var note = (column.Required ? "مطلوب. " : "") + (column.Hint ?? "") + (column.Options is { } o ? $" القيم: {string.Join(" / ", o)}" : "");
                if (!string.IsNullOrWhiteSpace(note))
                    cell.CreateComment().AddText(note.Trim());
                ws.Column(c + 1).Width = Math.Max(16, column.Header.Length + 6);

                // Dropdown: a closed set, or the names already in the system for a reference column
                var values = column.Options ?? ReferenceValues(sheet.Key, column.Key, references);
                if (values is { Length: > 0 })
                {
                    listColumn++;
                    for (var v = 0; v < values.Length; v++)
                        lists.Cell(v + 1, listColumn).Value = values[v];
                    var range = lists.Range(1, listColumn, values.Length, listColumn);
                    var validation = ws.Range(2, c + 1, 2000, c + 1).CreateDataValidation();
                    validation.List(range, true);
                    // A reference may also be a new name written in another sheet of the same file
                    validation.ShowErrorMessage = column.Options is not null;
                    validation.ErrorMessage = "اختار من القائمة";
                }
            }
            ws.Row(1).Height = 32;
            ws.SheetView.FreezeRows(1);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string[]? ReferenceValues(string sheet, string column, Dictionary<string, List<string>> references)
    {
        var source = (sheet, column) switch
        {
            ("departments", "parent") => "departments",
            ("departments", "branch") => "branches",
            ("positions", "department") or ("employees", "department") => "departments",
            ("positions", "jobtitle") or ("employees", "jobtitle") => "jobtitles",
            ("positions", "parent") or ("employees", "position") => "positions",
            ("employees", "shift") => "shifts",
            _ => null
        };
        return source is not null && references.TryGetValue(source, out var list) && list.Count > 0 ? list.ToArray() : null;
    }

    public static ImportFile Read(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var sheets = new Dictionary<string, List<ImportRow>>();
        var warnings = new List<string>();

        foreach (var ws in workbook.Worksheets)
        {
            var sheet = ImportSheets.All.FirstOrDefault(s => Same(s.Name, ws.Name));
            if (sheet is null)
                continue;

            // Columns are matched by header text, so their order may change
            var headerRow = ws.FirstRowUsed();
            if (headerRow is null)
                continue;
            var map = new Dictionary<int, string>();
            foreach (var cell in headerRow.CellsUsed())
            {
                var column = sheet.Columns.FirstOrDefault(c => Same(c.Header, cell.GetString()));
                if (column is not null)
                    map[cell.Address.ColumnNumber] = column.Key;
                else if (!string.IsNullOrWhiteSpace(cell.GetString()))
                    warnings.Add($"{sheet.Name}: العمود «{cell.GetString().Trim()}» مش معروف واتجاهل");
            }
            var missing = sheet.Columns.Where(c => c.Required && !map.ContainsValue(c.Key)).Select(c => c.Header).ToList();
            if (missing.Count > 0)
                warnings.Add($"{sheet.Name}: أعمدة مطلوبة ناقصة: {string.Join("، ", missing)}");

            var rows = new List<ImportRow>();
            foreach (var row in ws.RowsUsed().Where(r => r.RowNumber() > headerRow.RowNumber()))
            {
                var cells = new Dictionary<string, string>();
                foreach (var (number, key) in map)
                {
                    var text = CellText(row.Cell(number));
                    if (!string.IsNullOrWhiteSpace(text))
                        cells[key] = text.Trim();
                }
                if (cells.Count > 0)
                    rows.Add(new ImportRow(row.RowNumber(), cells));
            }
            if (rows.Count > 0)
                sheets[sheet.Key] = rows;
        }

        if (sheets.Count == 0)
            warnings.Add("الملف مفيهوش ولا شيت من شيتات القالب. نزّل القالب واملاه.");
        return new ImportFile(sheets, warnings);
    }

    // Excel keeps dates and times as numbers; give them the text forms the importer parses
    private static string CellText(IXLCell cell) => cell.DataType switch
    {
        XLDataType.DateTime => cell.GetDateTime() is var d && d.TimeOfDay == TimeSpan.Zero
            ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : d.Date == new DateTime(1899, 12, 30) || d.Date == new DateTime(1899, 12, 31) || d.Date == new DateTime(1900, 1, 1)
                ? d.ToString("HH:mm", CultureInfo.InvariantCulture)
                : d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        XLDataType.TimeSpan => cell.GetTimeSpan().ToString(@"hh\:mm"),
        XLDataType.Number => cell.GetDouble().ToString("0.############", CultureInfo.InvariantCulture),
        XLDataType.Boolean => cell.GetBoolean() ? "أيوه" : "لأ",
        _ => cell.GetFormattedString()
    };

    private static bool Same(string a, string b) => Normalize(a) == Normalize(b);

    private static string Normalize(string s) => s.Replace("*", "").Replace("ـ", "").Trim();
}
