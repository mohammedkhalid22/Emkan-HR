using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using HRSystem.BLL.DTOs;

namespace HRSystem.Web.Services;

// Reads a fingerprint export: columns Code, Date, CheckIn, CheckOut (CSV or Excel, header row optional)
public static class AttendanceFileReader
{
    public static readonly string[] Extensions = [".csv", ".xlsx"];

    public const string SampleCsv = "Code,Date,CheckIn,CheckOut\nEMP-0001,2026-10-25,08:55,17:05\nEMP-0002,2026-10-25,09:20,17:00\n";

    public static List<AttendanceImportRow> Read(Stream stream, string fileName)
    {
        var rows = Path.GetExtension(fileName).ToLowerInvariant() == ".xlsx" ? ReadExcel(stream) : ReadCsv(stream);

        // Drop a header row (its date column isn't a date)
        if (rows.Count > 0 && !LooksLikeDate(rows[0].Date))
            rows.RemoveAt(0);

        return rows.Where(r => !string.IsNullOrWhiteSpace(r.Code) || !string.IsNullOrWhiteSpace(r.Date)).ToList();
    }

    private static List<AttendanceImportRow> ReadCsv(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var rows = new List<AttendanceImportRow>();
        var lineNumber = 0;

        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            var cells = line.Split([',', ';', '\t']).Select(c => c.Trim().Trim('"')).ToArray();
            rows.Add(new AttendanceImportRow(lineNumber, Cell(cells, 0), Cell(cells, 1), Cell(cells, 2), Cell(cells, 3)));
        }
        return rows;
    }

    private static List<AttendanceImportRow> ReadExcel(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.First();
        var rows = new List<AttendanceImportRow>();

        foreach (var row in sheet.RowsUsed())
        {
            rows.Add(new AttendanceImportRow(
                row.RowNumber(),
                row.Cell(1).GetFormattedString(),
                DateText(row.Cell(2)),
                TimeText(row.Cell(3)),
                TimeText(row.Cell(4))));
        }
        return rows;
    }

    // Excel stores dates and times as numbers; turn them into the text formats the service expects
    private static string DateText(IXLCell cell)
        => cell.DataType == XLDataType.DateTime ? cell.GetDateTime().ToString("yyyy-MM-dd") : cell.GetFormattedString();

    private static string TimeText(IXLCell cell) => cell.DataType switch
    {
        XLDataType.DateTime => cell.GetDateTime().ToString("HH:mm"),
        XLDataType.TimeSpan => cell.GetTimeSpan().ToString(@"hh\:mm"),
        XLDataType.Number => TimeSpan.FromDays(cell.GetDouble()).ToString(@"hh\:mm"),
        _ => cell.GetFormattedString()
    };

    private static string? Cell(string[] cells, int index) => index < cells.Length ? cells[index] : null;

    private static bool LooksLikeDate(string? text)
        => DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}
