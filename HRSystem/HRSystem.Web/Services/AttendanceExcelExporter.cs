using ClosedXML.Excel;
using HRSystem.BLL.DTOs;

namespace HRSystem.Web.Services;

public static class AttendanceExcelExporter
{
    public static byte[] ExportMonthly(IReadOnlyList<MonthlyAttendanceRowDto> rows, int year, int month)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add($"حضور {year}-{month:D2}");
        sheet.RightToLeft = true;

        string[] headers = ["الكود", "الاسم", "القسم", "حضور", "غياب", "إجازة", "مأمورية", "عطلة", "دقايق تأخير", "دقايق انصراف مبكر", "دقايق أوفر تايم"];
        for (var i = 0; i < headers.Length; i++)
            sheet.Cell(1, i + 1).Value = headers[i];

        var header = sheet.Range(1, 1, 1, headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");

        var r = 2;
        foreach (var row in rows)
        {
            sheet.Cell(r, 1).Value = row.EmployeeCode;
            sheet.Cell(r, 2).Value = row.EmployeeName;
            sheet.Cell(r, 3).Value = row.DepartmentName;
            sheet.Cell(r, 4).Value = row.PresentDays;
            sheet.Cell(r, 5).Value = row.AbsentDays;
            sheet.Cell(r, 6).Value = row.LeaveDays;
            sheet.Cell(r, 7).Value = row.MissionDays;
            sheet.Cell(r, 8).Value = row.HolidayDays;
            sheet.Cell(r, 9).Value = row.LateMinutes;
            sheet.Cell(r, 10).Value = row.EarlyLeaveMinutes;
            sheet.Cell(r, 11).Value = row.OvertimeMinutes;
            r++;
        }

        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(1);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
