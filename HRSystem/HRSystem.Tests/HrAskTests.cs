using HRSystem.BLL.Common;

namespace HRSystem.Tests;

public class HrAskTests
{
    [Fact]
    public void Table_has_title_header_and_tab_separated_rows()
    {
        var text = HrAsk.Table("Employees", ["Code", "Name"], [["EMP-1", "منى"], ["EMP-2", "أحمد"]]);

        Assert.Contains("## Employees", text);
        Assert.Contains("Code\tName", text);
        Assert.Contains("EMP-2\tأحمد", text);
    }

    [Fact]
    public void Empty_table_says_so()
        => Assert.Contains("(no rows)", HrAsk.Table("Loans", ["Code"], []));

    [Fact]
    public void Cells_cannot_break_the_table_layout()
        => Assert.Equal("line one  line two", HrAsk.Cell("line one\n\tline two"));

    [Fact]
    public void Cells_use_invariant_formats()
    {
        Assert.Equal("2026-11-03", HrAsk.Cell(new DateOnly(2026, 11, 3)));
        Assert.Equal("09:05", HrAsk.Cell(new TimeOnly(9, 5)));
        Assert.Equal("1234.5", HrAsk.Cell(1234.50m));
        Assert.Equal("", HrAsk.Cell(null));
    }

    [Fact]
    public void Reads_an_answer_with_a_table()
    {
        var answer = HrAsk.ParseAnswer("""
            ```json
            {"answer": "اتنين اتأخروا", "table": {"columns": ["الاسم", "مرات التأخير"], "rows": [["منى", 4], ["أحمد"]]}}
            ```
            """)!;

        Assert.Equal("اتنين اتأخروا", answer.Text);
        Assert.Equal(["الاسم", "مرات التأخير"], answer.Columns);
        Assert.Equal(["منى", "4"], answer.Rows[0]);
        Assert.Equal(["أحمد", ""], answer.Rows[1]);
    }

    [Fact]
    public void A_table_without_rows_is_dropped()
    {
        var answer = HrAsk.ParseAnswer("""{"answer": "مفيش حد", "table": {"columns": ["الاسم"], "rows": []}}""")!;
        Assert.Empty(answer.Columns);
        Assert.Empty(answer.Rows);
    }

    [Theory]
    [InlineData("""{"table": null}""")]
    [InlineData("""{"answer": "  "}""")]
    [InlineData("not json")]
    public void Unusable_replies_are_rejected(string reply)
        => Assert.Null(HrAsk.ParseAnswer(reply));
}
