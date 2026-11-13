using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using HRSystem.BLL.Common;

namespace HRSystem.Web.Services.DataImport;

// ---------- Any workbook, as found ----------

public record RawColumn(int Index, string Header, List<string> Samples, List<string> Distinct);

public record RawSheet(string Name, int HeaderRow, List<RawColumn> Columns, List<(int Row, Dictionary<int, string> Cells)> Rows);

// ---------- How a workbook maps onto the import sheets ----------

public class ImportMapping
{
    public List<SheetMapping> Sheets { get; set; } = [];

    // Departments and job titles named in the employees' rows but missing everywhere are created
    public bool CreateMissing { get; set; } = true;

    // Set when the AI drew the mapping (or why it could not)
    public string? AiNote { get; set; }
}

public class SheetMapping
{
    public string Source { get; set; } = null!;
    public string? Target { get; set; }

    // source header → column key of the target sheet
    public Dictionary<string, string> Columns { get; set; } = [];

    // column key → (value in the file → value the program understands)
    public Dictionary<string, Dictionary<string, string>> Values { get; set; } = [];
}

// Reads a workbook of any shape, guesses which sheet and column is which, lets the AI improve
// the guess, then turns it into the same ImportFile the template gives
public static class ImportMapper
{
    private const int SampleCount = 3;
    private const int MaxDistinct = 15;

    public static List<RawSheet> ReadRaw(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var sheets = new List<RawSheet>();
        foreach (var ws in workbook.Worksheets.Where(w => w.Visibility == XLWorksheetVisibility.Visible))
        {
            var used = ws.RangeUsed();
            if (used is null)
                continue;

            // The header row: among the first rows, the one with the most text cells
            var header = ws.Rows(used.FirstRow().RowNumber(), Math.Min(used.FirstRow().RowNumber() + 9, used.LastRow().RowNumber()))
                .Select(r => new { Row = r, Text = r.CellsUsed().Count(c => c.DataType == XLDataType.Text && c.GetString().Trim().Length > 1) })
                .Where(r => r.Text >= 2)
                .OrderByDescending(r => r.Text).ThenBy(r => r.Row.RowNumber())
                .FirstOrDefault()?.Row;
            if (header is null)
                continue;

            var headers = header.CellsUsed()
                .Where(c => !string.IsNullOrWhiteSpace(c.GetString()))
                .Select(c => (Index: c.Address.ColumnNumber, Text: c.GetString().Trim()))
                .ToList();

            var rows = new List<(int, Dictionary<int, string>)>();
            foreach (var row in ws.RowsUsed().Where(r => r.RowNumber() > header.RowNumber()))
            {
                var cells = new Dictionary<int, string>();
                foreach (var (index, _) in headers)
                {
                    var text = ImportWorkbook.CellText(row.Cell(index)).Trim();
                    if (text.Length > 0)
                        cells[index] = text;
                }
                if (cells.Count > 0)
                    rows.Add((row.RowNumber(), cells));
            }
            if (rows.Count == 0)
                continue;

            var columns = headers.Select(h =>
            {
                var values = rows.Select(r => r.Item2.GetValueOrDefault(h.Index)).Where(v => v is not null).Select(v => v!).ToList();
                var distinct = values.Distinct().ToList();
                return new RawColumn(h.Index, h.Text, values.Take(SampleCount).ToList(), distinct.Count <= MaxDistinct ? distinct : []);
            }).ToList();
            sheets.Add(new RawSheet(ws.Name, header.RowNumber(), columns, rows));
        }
        return sheets;
    }

    // ---------- First guess, no AI ----------

    // Other ways people title the same column
    private static readonly Dictionary<string, string[]> Synonyms = new()
    {
        ["employees.nationalid"] = ["الرقم القومي", "رقم قومي", "national id", "nationalid", "national no", "id number", "رقم البطاقة", "الرقم القومى"],
        ["employees.namear"] = ["الاسم", "اسم الموظف", "الاسم رباعي", "الاسم بالكامل", "الاسم بالعربي", "employee name", "name", "full name"],
        ["employees.nameen"] = ["الاسم بالانجليزي", "الاسم بالإنجليزي", "english name", "name (en)"],
        ["employees.phone"] = ["الموبايل", "الهاتف", "رقم الموبايل", "التليفون", "mobile", "phone", "tel"],
        ["employees.email"] = ["البريد", "الايميل", "الإيميل", "email", "e-mail", "mail"],
        ["employees.hiredate"] = ["تاريخ التعيين", "تاريخ الالتحاق", "تاريخ التعين", "hire date", "hiring date", "join date", "start date"],
        ["employees.salary"] = ["المرتب", "الراتب", "المرتب الأساسي", "الراتب الأساسي", "الأساسي", "basic salary", "salary", "basic"],
        ["employees.department"] = ["القسم", "الإدارة", "الادارة", "department", "dept"],
        ["employees.jobtitle"] = ["الوظيفة", "المسمى", "المسمى الوظيفي", "job title", "title", "position", "job"],
        ["employees.contract"] = ["نوع العقد", "العقد", "contract", "contract type"],
        ["employees.bankaccount"] = ["رقم الحساب", "الحساب البنكي", "account", "bank account"],
        ["employees.fingerprint"] = ["رقم البصمة", "كود البصمة", "fingerprint", "enroll id", "user id"],
        ["employees.insuranceno"] = ["الرقم التأميني", "رقم التأمين", "insurance no", "insurance number"],
        ["departments.name"] = ["القسم", "الإدارة", "اسم القسم", "department"],
        ["jobtitles.name"] = ["الوظيفة", "المسمى", "job title"],
        ["branches.name"] = ["الفرع", "اسم الفرع", "branch"],
        ["shifts.name"] = ["الوردية", "shift"],
        ["holidays.date"] = ["التاريخ", "date"],
        ["holidays.name"] = ["العطلة", "المناسبة", "holiday"],
    };

    // Common ways files write the program's closed values (keys normalized like Norm)
    // The same word may mean different things per column ("ثابت"), so the first meaning that column allows wins
    private static readonly ILookup<string, string> ValueSynonyms = new[]
    {
        ("full time", "دائم"), ("fulltime", "دائم"), ("permanent", "دائم"), ("دوام كامل", "دائم"), ("ثابت", "دائم"), ("معين", "دائم"),
        ("part time", "دوام جزئي"), ("parttime", "دوام جزئي"), ("partial", "دوام جزئي"), ("جزئي", "دوام جزئي"),
        ("temporary", "مؤقت"), ("temp", "مؤقت"), ("contract", "مؤقت"), ("محدد المدة", "مؤقت"), ("عقد مؤقت", "مؤقت"),
        ("single", "أعزب"), ("عزباء", "أعزب"), ("married", "متزوج"), ("متزوجة", "متزوج"), ("divorced", "مطلق"), ("مطلقة", "مطلق"), ("widowed", "أرمل"), ("أرملة", "أرمل"),
        ("muslim", "مسلم"), ("مسلمة", "مسلم"), ("christian", "مسيحي"), ("مسيحية", "مسيحي"),
        ("completed", "أدى الخدمة"), ("أدى", "أدى الخدمة"), ("exempted", "معفى"), ("exempt", "معفى"), ("معفي", "معفى"), ("postponed", "مؤجل"),
        ("yes", "أيوه"), ("y", "أيوه"), ("نعم", "أيوه"), ("true", "أيوه"), ("no", "لأ"), ("n", "لأ"), ("لا", "لأ"), ("false", "لأ"),
        ("allowance", "بدل / استحقاق"), ("بدل", "بدل / استحقاق"), ("deduction", "استقطاع"), ("خصم", "استقطاع"),
        ("fixed", "مبلغ ثابت"), ("ثابت", "مبلغ ثابت"), ("percentage", "نسبة من الأساسي"), ("percent", "نسبة من الأساسي"), ("نسبة", "نسبة من الأساسي"),
        ("department", "قسم"), ("section", "قسم"), ("administration", "إدارة"), ("division", "إدارة"),
    }.ToLookup(p => Norm(p.Item1), p => p.Item2);

    public static ImportMapping Guess(List<RawSheet> raw)
    {
        var mapping = new ImportMapping();
        foreach (var sheet in raw)
        {
            // The target whose columns this sheet matches best (or whose name it carries)
            var scored = ImportSheets.All
                .Select(t => new { Target = t, Columns = MatchColumns(sheet, t), NameHit = Similar(sheet.Name, t.Name) })
                .OrderByDescending(x => x.Columns.Count + (x.NameHit ? 2 : 0))
                .First();
            var map = new SheetMapping { Source = sheet.Name };
            if (scored.Columns.Count >= 2 || scored.NameHit)
            {
                map.Target = scored.Target.Key;
                map.Columns = scored.Columns;
            }
            mapping.Sheets.Add(map);
        }
        FillValues(raw, mapping);
        return mapping;
    }

    private static Dictionary<string, string> MatchColumns(RawSheet sheet, ImportSheet target)
    {
        var result = new Dictionary<string, string>();
        foreach (var column in sheet.Columns)
        {
            var header = Norm(column.Header);
            var hit = target.Columns.FirstOrDefault(c => !result.ContainsValue(c.Key)
                && (Norm(c.Header) == header || Synonyms.GetValueOrDefault($"{target.Key}.{c.Key}", []).Any(s => Norm(s) == header)));
            if (hit is not null)
                result[column.Header] = hit.Key;
        }
        return result;
    }

    // Values the file writes differently from the program ("M", "Full time"...) mapped when obvious
    private static void FillValues(List<RawSheet> raw, ImportMapping mapping)
    {
        foreach (var map in mapping.Sheets.Where(m => m.Target is not null))
        {
            var target = ImportSheets.Find(map.Target!)!;
            var sheet = raw.First(s => s.Name == map.Source);
            foreach (var (header, key) in map.Columns)
            {
                var options = target.Columns.First(c => c.Key == key).Options;
                var column = sheet.Columns.FirstOrDefault(c => c.Header == header);
                if (options is null || column is null)
                    continue;
                var values = map.Values.TryGetValue(key, out var existing) ? existing : map.Values[key] = [];
                foreach (var value in column.Distinct.Where(v => !values.ContainsKey(v)))
                {
                    var exact = options.FirstOrDefault(o => Norm(o) == Norm(value))
                        ?? ValueSynonyms[Norm(value)].FirstOrDefault(options.Contains);
                    if (exact is not null)
                        values[value] = exact;
                }
            }
        }
    }

    // ---------- The AI's guess ----------

    public static async Task<ImportMapping> AskAiAsync(List<RawSheet> raw, ImportMapping guess, IAiClient ai, CancellationToken cancellationToken = default)
    {
        if (!ai.IsConfigured)
        {
            guess.AiNote = "الذكاء الاصطناعي مش متفعّل على السيرفر، فالربط ده من أسماء الأعمدة بس";
            return guess;
        }

        var reply = await ai.GenerateJsonAsync(Instructions, Prompt(raw, guess), null, cancellationToken);
        if (!reply.Succeeded)
        {
            guess.AiNote = "الذكاء الاصطناعي مارجعش (" + reply.Error + ")، فالربط ده من أسماء الأعمدة بس";
            return guess;
        }

        try
        {
            using var doc = JsonDocument.Parse(reply.Value!);
            var mapping = new ImportMapping { CreateMissing = guess.CreateMissing };
            foreach (var sheet in raw)
            {
                var map = new SheetMapping { Source = sheet.Name };
                var answer = doc.RootElement.GetProperty("sheets").EnumerateArray()
                    .FirstOrDefault(s => s.TryGetProperty("source", out var src) && src.GetString() == sheet.Name);
                if (answer.ValueKind == JsonValueKind.Object
                    && answer.TryGetProperty("target", out var t) && t.ValueKind == JsonValueKind.String
                    && ImportSheets.Find(t.GetString()!) is { } target)
                {
                    map.Target = target.Key;
                    if (answer.TryGetProperty("columns", out var cols) && cols.ValueKind == JsonValueKind.Object)
                        foreach (var col in cols.EnumerateObject())
                            if (col.Value.ValueKind == JsonValueKind.String && target.Columns.Any(c => c.Key == col.Value.GetString())
                                && sheet.Columns.Any(c => c.Header == col.Name) && !map.Columns.ContainsValue(col.Value.GetString()!))
                                map.Columns[col.Name] = col.Value.GetString()!;
                    if (answer.TryGetProperty("values", out var vals) && vals.ValueKind == JsonValueKind.Object)
                        foreach (var col in vals.EnumerateObject())
                        {
                            var options = target.Columns.FirstOrDefault(c => c.Key == col.Name)?.Options;
                            if (options is null || col.Value.ValueKind != JsonValueKind.Object)
                                continue;
                            var values = map.Values[col.Name] = [];
                            foreach (var v in col.Value.EnumerateObject())
                                if (v.Value.ValueKind == JsonValueKind.String && options.Contains(v.Value.GetString()))
                                    values[v.Name] = v.Value.GetString()!;
                        }
                }
                else
                {
                    // The AI had no idea: keep the name-based guess for this sheet
                    map = guess.Sheets.First(s => s.Source == sheet.Name);
                }
                mapping.Sheets.Add(map);
            }
            FillValues(raw, mapping);
            mapping.AiNote = "الذكاء الاصطناعي ربط الأعمدة. راجعها قبل ما تكمّل";
            return mapping;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            guess.AiNote = "رد الذكاء الاصطناعي مش مفهوم، فالربط ده من أسماء الأعمدة بس";
            return guess;
        }
    }

    private const string Instructions = """
        You map an HR company's Excel workbook onto the import sheets of an HR system.
        Reply with JSON only:
        {"sheets":[{"source":"<sheet name exactly as given>","target":"<target sheet key or null>",
          "columns":{"<source header exactly as given>":"<target column key>"},
          "values":{"<target column key>":{"<value as in the file>":"<one of that column's allowed values>"}}}]}
        Rules:
        - Use only the target sheet keys and column keys listed. Leave out any column you cannot place.
        - Each target column key at most once per sheet.
        - "values" only for target columns that list allowed values, mapping every distinct value given for the mapped source column.
        - A sheet that is not HR basic data (totals, notes) gets "target": null.
        - Sample values are partly masked on purpose (X and ***); judge by headers and the shape of the values.
        """;

    private static string Prompt(List<RawSheet> raw, ImportMapping guess)
    {
        var sb = new StringBuilder("TARGET SHEETS\n");
        foreach (var t in ImportSheets.All)
        {
            sb.Append($"- {t.Key} ({t.Name}): ");
            sb.AppendJoin("; ", t.Columns.Select(c => $"{c.Key} = {c.Header}{(c.Options is { } o ? " [allowed: " + string.Join(" | ", o) + "]" : "")}{(c.Hint is { } h ? " (" + h + ")" : "")}"));
            sb.Append('\n');
        }
        sb.Append("\nTHE WORKBOOK\n");
        foreach (var sheet in raw)
        {
            sb.Append($"Sheet \"{sheet.Name}\" ({sheet.Rows.Count} rows). Columns:\n");
            foreach (var c in sheet.Columns)
            {
                sb.Append($"  - \"{c.Header}\": e.g. {string.Join(" | ", c.Samples.Select(Mask))}");
                if (c.Distinct.Count is > 0 and <= MaxDistinct && c.Distinct.Count < sheet.Rows.Count)
                    sb.Append($"; distinct values: {string.Join(" | ", c.Distinct.Select(Mask))}");
                sb.Append('\n');
            }
        }
        return sb.ToString();
    }

    // Long digit runs (IDs, phones, accounts) and names never leave as they are
    public static string Mask(string value)
    {
        var v = value.Trim();
        if (Regex.IsMatch(v, @"^\+?[\d\s-]{8,}$"))
        {
            var digits = Regex.Replace(v, @"\D", "");
            if (digits.Length < 4)
                return "XXXX";
            return digits[..2] + new string('X', digits.Length - 3) + digits[^1];
        }
        if (v.Contains('@'))
            return "***" + v[v.IndexOf('@')..];
        // Two or more words of letters: likely a person's name, keep the first word only
        var words = v.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2 && words.All(w => w.All(char.IsLetter)))
            return words[0] + " ***";
        return v.Length > 40 ? v[..40] + "…" : v;
    }

    // ---------- Turning the workbook into import rows ----------

    // existing = names already in the system ("departments", "jobtitles"), so only missing ones are added
    public static ImportFile Convert(List<RawSheet> raw, ImportMapping mapping, Dictionary<string, List<string>> existing)
    {
        var sheets = new Dictionary<string, List<ImportRow>>();
        var warnings = new List<string>();
        var offset = 0;

        foreach (var map in mapping.Sheets.Where(m => m.Target is not null))
        {
            var sheet = raw.FirstOrDefault(s => s.Name == map.Source);
            var target = ImportSheets.Find(map.Target!);
            if (sheet is null || target is null)
                continue;

            var missing = target.Columns.Where(c => c.Required && !map.Columns.ContainsValue(c.Key)).Select(c => c.Header).ToList();
            if (missing.Count > 0)
                warnings.Add($"«{sheet.Name}» → {target.Name}: مفيش عمود لـ {string.Join("، ", missing)}");

            var rows = sheets.TryGetValue(target.Key, out var list) ? list : sheets[target.Key] = [];
            // Row numbers stay those of the file; sheets mapped to the same target are kept apart
            offset += 100000;
            foreach (var (number, cells) in sheet.Rows)
            {
                var values = new Dictionary<string, string>();
                foreach (var column in sheet.Columns)
                {
                    if (!map.Columns.TryGetValue(column.Header, out var key) || !cells.TryGetValue(column.Index, out var value))
                        continue;
                    if (map.Values.TryGetValue(key, out var translate) && translate.TryGetValue(value, out var known))
                        value = known;
                    values[key] = value;
                }
                if (values.Count > 0)
                    rows.Add(new ImportRow(offset + number, values));
            }
        }

        if (mapping.CreateMissing && sheets.TryGetValue("employees", out var employees))
        {
            AddMissing(sheets, employees, "department", "departments", existing, name => new() { ["name"] = name, ["type"] = Helpers.Labels.Of(Domain.Enums.DepartmentType.Administration) });
            AddMissing(sheets, employees, "jobtitle", "jobtitles", existing, name => new() { ["name"] = name });
        }

        foreach (var key in sheets.Where(s => s.Value.Count == 0).Select(s => s.Key).ToList())
            sheets.Remove(key);
        if (sheets.Count == 0)
            warnings.Add("مفيش ولا شيت مربوط بشاشة من البرنامج");
        return new ImportFile(sheets, warnings);
    }

    // Departments / job titles named by employees that the file and the system both lack
    private static void AddMissing(Dictionary<string, List<ImportRow>> sheets, List<ImportRow> employees, string column, string target,
        Dictionary<string, List<string>> existing, Func<string, Dictionary<string, string>> make)
    {
        var known = new HashSet<string>(existing.GetValueOrDefault(target) ?? [], StringComparer.OrdinalIgnoreCase);
        var rows = sheets.TryGetValue(target, out var list) ? list : sheets[target] = [];
        foreach (var r in rows)
            if (r.Get("name") is { } n)
                known.Add(n);

        var number = 9_000_000;
        foreach (var name in employees.Select(e => e.Get(column)).Where(n => n is not null).Distinct())
            if (known.Add(name!))
                rows.Add(new ImportRow(++number, make(name!)));
    }

    private static bool Similar(string a, string b) => Norm(a).Contains(Norm(b)) || Norm(b).Contains(Norm(a));

    private static string Norm(string s)
        => Regex.Replace(ImportWorkbook.Normalize(s).ToLowerInvariant().Replace("أ", "ا").Replace("إ", "ا").Replace("آ", "ا").Replace("ة", "ه").Replace("ى", "ي"), @"[\s_\-().:]", "");
}
