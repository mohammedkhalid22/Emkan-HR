using ClosedXML.Excel;
using HRSystem.BLL.DTOs;
using HRSystem.BLL.Services;
using HRSystem.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRSystem.Web.Controllers;

// One page with every report the system can export
[Authorize(Roles = Roles.AdminOrHR)]
public class ReportsController : Controller
{
    private readonly IPayrollService _payrollService;
    private readonly ILeaveBalanceService _balanceService;

    public ReportsController(IPayrollService payrollService, ILeaveBalanceService balanceService)
    {
        _payrollService = payrollService;
        _balanceService = balanceService;
    }

    public async Task<IActionResult> Index() => View(await _payrollService.GetRunsAsync());

    public async Task<IActionResult> LeaveBalances(int year)
    {
        var rows = await _balanceService.GetAsync(new LeaveBalanceFilterDto { Year = year });

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add($"أرصدة {year}");
        sheet.RightToLeft = true;

        string[] headers = ["الكود", "الاسم", "القسم", "النوع", "الرصيد", "المستخدم", "معلق", "المتاح"];
        for (var c = 0; c < headers.Length; c++)
            sheet.Cell(1, c + 1).Value = headers[c];
        sheet.Row(1).Style.Font.Bold = true;

        var r = 2;
        foreach (var b in rows)
        {
            sheet.Cell(r, 1).Value = b.EmployeeCode;
            sheet.Cell(r, 2).Value = b.EmployeeName;
            sheet.Cell(r, 3).Value = b.DepartmentName;
            sheet.Cell(r, 4).Value = b.LeaveTypeName;
            sheet.Cell(r, 5).Value = b.Entitled;
            sheet.Cell(r, 6).Value = b.Used;
            sheet.Cell(r, 7).Value = b.Pending;
            sheet.Cell(r, 8).Value = b.Available;
            r++;
        }
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"leave-balances-{year}.xlsx");
    }
}
