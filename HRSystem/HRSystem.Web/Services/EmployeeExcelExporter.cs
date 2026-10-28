using ClosedXML.Excel;
using HRSystem.BLL.DTOs;
using HRSystem.Web.Helpers;

namespace HRSystem.Web.Services;

public static class EmployeeExcelExporter
{
    public static byte[] Export(IReadOnlyList<EmployeeListItemDto> employees)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("الموظفين");
        sheet.RightToLeft = true;

        string[] headers = ["الكود", "الاسم", "الرقم القومي", "القسم", "المسمى الوظيفي", "المدير", "الموبايل", "تاريخ التعيين", "الحالة"];
        for (var i = 0; i < headers.Length; i++)
            sheet.Cell(1, i + 1).Value = headers[i];

        var header = sheet.Range(1, 1, 1, headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");

        var row = 2;
        foreach (var e in employees)
        {
            sheet.Cell(row, 1).Value = e.Code;
            sheet.Cell(row, 2).Value = e.FullNameAr;
            // Stored as text so Excel doesn't turn the 14 digits into 2.9E+13
            sheet.Cell(row, 3).SetValue(e.NationalId).Style.NumberFormat.Format = "@";
            sheet.Cell(row, 4).Value = e.DepartmentName;
            sheet.Cell(row, 5).Value = e.JobTitleName;
            sheet.Cell(row, 6).Value = e.ManagerName ?? "";
            sheet.Cell(row, 7).SetValue(e.Phone ?? "");
            sheet.Cell(row, 8).Value = e.HireDate.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(row, 8).Style.DateFormat.Format = "yyyy-mm-dd";
            sheet.Cell(row, 9).Value = Labels.Of(e.Status);
            row++;
        }

        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(1);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
