using HRSystem.BLL.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HRSystem.Web.Services;

// Payslip as an A5 PDF, right-to-left Arabic
public static class PayslipPdf
{
    // A Windows font with Arabic letters; QuestPDF picks it up from the installed fonts
    private const string Font = "Tahoma";

    public static byte[] Generate(PayslipDto slip, string companyName)
    {
        var i = slip.Item;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(24);
                page.ContentFromRightToLeft();
                page.DefaultTextStyle(t => t.FontFamily(Font).FontSize(9.5f));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(companyName).FontSize(13).Bold();
                        row.ConstantItem(150).AlignLeft().Text($"كشف مرتب {slip.Month:D2}/{slip.Year}").FontSize(11).Bold();
                    });
                    if (slip.RunStatus != Domain.Enums.PayrollRunStatus.Approved)
                        col.Item().PaddingTop(4).Text("مسودة: المسير لسه ما اتعتمدش").FontColor(Colors.Red.Medium);
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(2); c.RelativeColumn(); c.RelativeColumn(2); });
                        Info(table, "الموظف", i.EmployeeName);
                        Info(table, "الكود", i.EmployeeCode);
                        Info(table, "القسم", i.DepartmentName);
                        Info(table, "المسمى", i.JobTitleName);
                        Info(table, "أيام المرتب", i.SalaryDays.ToString());
                        Info(table, "أيام الغياب", i.AbsentDays.ToString("0.#"));
                        Info(table, "دقايق تأخير", i.LateMinutes.ToString());
                        Info(table, "دقايق أوفر تايم", i.OvertimeMinutes.ToString());
                    });

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Element(c => Section(c, "الاستحقاقات", Colors.Green.Lighten4,
                        [
                            ("المرتب الأساسي", i.BasicSalary),
                            ("البدلات", i.TotalAllowances),
                            ("الأوفر تايم", i.OvertimeAmount),
                            ("المكافآت", i.Bonuses)
                        ], ("الإجمالي", i.GrossSalary)));

                        row.ConstantItem(12);

                        row.RelativeItem().Element(c => Section(c, "الاستقطاعات", Colors.Red.Lighten4,
                        [
                            ("التأمينات (حصة الموظف)", i.InsuranceEmployee),
                            ("ضريبة كسب العمل", i.IncomeTax),
                            ("الغياب", i.AbsenceDeduction),
                            ("التأخير والانصراف المبكر", i.LateDeduction),
                            ("الجزاءات", i.Penalties),
                            ("قسط السلفة", i.LoanInstallment),
                            ("استقطاعات أخرى", i.OtherDeductions)
                        ], ("إجمالي الاستقطاعات", i.TotalDeductions)));
                    });

                    col.Item().Background(Colors.Blue.Lighten5).Padding(10).Row(row =>
                    {
                        row.RelativeItem().Text("صافي المرتب").FontSize(12).Bold();
                        row.ConstantItem(140).AlignLeft().Text($"{i.NetSalary:N2} جنيه").FontSize(13).Bold();
                    });

                    col.Item().Text($"حصة الشركة في التأمينات: {i.InsuranceCompany:N2} جنيه").FontColor(Colors.Grey.Darken1).FontSize(8);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(Colors.Grey.Medium));
                    t.Span($"اتطبع {DateTime.Now:yyyy-MM-dd HH:mm} من إمكان HR");
                });
            });
        }).GeneratePdf();
    }

    private static void Info(TableDescriptor table, string label, string value)
    {
        table.Cell().PaddingVertical(2).Text(label).FontColor(Colors.Grey.Darken1);
        table.Cell().PaddingVertical(2).Text(value).SemiBold();
    }

    private static void Section(IContainer container, string title, string headerColor,
        (string Label, decimal Amount)[] lines, (string Label, decimal Amount) total)
    {
        container.Border(0.5f).BorderColor(Colors.Grey.Lighten1).Column(col =>
        {
            col.Item().Background(headerColor).Padding(5).Text(title).Bold();

            foreach (var (label, amount) in lines.Where(l => l.Amount != 0))
            {
                col.Item().PaddingHorizontal(5).PaddingVertical(3).Row(row =>
                {
                    row.RelativeItem().Text(label);
                    row.ConstantItem(70).AlignLeft().Text(amount.ToString("N2"));
                });
            }

            col.Item().BorderTop(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(5).Row(row =>
            {
                row.RelativeItem().Text(total.Label).Bold();
                row.ConstantItem(70).AlignLeft().Text(total.Amount.ToString("N2")).Bold();
            });
        });
    }
}
