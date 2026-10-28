using ClosedXML.Excel;
using HRSystem.BLL.DTOs;

namespace HRSystem.Web.Services;

public static class PayrollExcelExporter
{
    // Full payroll register: one row per employee with every amount
    public static byte[] Register(PayrollRunDetailsDto run)
    {
        string[] headers = ["الكود", "الاسم", "القسم", "أيام المرتب", "الأساسي", "البدلات", "أوفر تايم", "مكافآت", "الإجمالي",
            "تأمينات موظف", "ضريبة", "غياب", "تأخير", "جزاءات", "سلفة", "استقطاعات أخرى", "الصافي", "تأمينات شركة"];

        return Build($"مسير {run.Run.Year}-{run.Run.Month:D2}", headers, run.Items, (sheet, r, i) =>
        {
            object[] values = [i.EmployeeCode, i.EmployeeName, i.DepartmentName, i.SalaryDays, i.BasicSalary, i.TotalAllowances,
                i.OvertimeAmount, i.Bonuses, i.GrossSalary, i.InsuranceEmployee, i.IncomeTax, i.AbsenceDeduction, i.LateDeduction,
                i.Penalties, i.LoanInstallment, i.OtherDeductions, i.NetSalary, i.InsuranceCompany];
            for (var c = 0; c < values.Length; c++)
                sheet.Cell(r, c + 1).Value = XLCellValue.FromObject(values[c]);
        }, firstMoneyColumn: 5);
    }

    // What the bank needs to transfer salaries: name, account, net
    public static byte[] BankFile(PayrollRunDetailsDto run)
    {
        string[] headers = ["الكود", "الاسم", "رقم الحساب", "صافي المرتب"];

        return Build("تحويل البنك", headers, run.Items, (sheet, r, i) =>
        {
            sheet.Cell(r, 1).Value = i.EmployeeCode;
            sheet.Cell(r, 2).Value = i.EmployeeName;
            sheet.Cell(r, 3).SetValue(i.BankAccount ?? "");
            sheet.Cell(r, 4).Value = i.NetSalary;
            if (string.IsNullOrWhiteSpace(i.BankAccount))
                sheet.Row(r).Style.Fill.BackgroundColor = XLColor.FromHtml("#FDE2E1");
        }, firstMoneyColumn: 4);
    }

    private static byte[] Build(string sheetName, string[] headers, List<PayrollItemDto> items,
        Action<IXLWorksheet, int, PayrollItemDto> writeRow, int firstMoneyColumn)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);
        sheet.RightToLeft = true;

        for (var c = 0; c < headers.Length; c++)
            sheet.Cell(1, c + 1).Value = headers[c];
        var header = sheet.Range(1, 1, 1, headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");

        var row = 2;
        foreach (var item in items)
            writeRow(sheet, row++, item);

        // Totals row under the money columns
        sheet.Cell(row, 1).Value = "الإجمالي";
        sheet.Row(row).Style.Font.Bold = true;
        for (var c = firstMoneyColumn; c <= headers.Length; c++)
        {
            var letter = sheet.Column(c).ColumnLetter();
            sheet.Cell(row, c).FormulaA1 = $"SUM({letter}2:{letter}{row - 1})";
        }

        sheet.Range(2, firstMoneyColumn, row, headers.Length).Style.NumberFormat.Format = "#,##0.00";
        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(1);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
