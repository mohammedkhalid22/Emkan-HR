using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRSystem.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AbsentDays",
                table: "PayrollItems",
                type: "decimal(5,1)",
                precision: 5,
                scale: 1,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "LateMinutes",
                table: "PayrollItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OvertimeMinutes",
                table: "PayrollItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SalaryDays",
                table: "PayrollItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Starting values so payroll can run; the accountant must review them in the settings screen.
            // Annual income tax brackets (law 30/2023, simplified: the rules that remove lower brackets for
            // high incomes are not applied) and the annual personal exemption.
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [TaxBrackets] WHERE [Year] = 2026)
                INSERT INTO [TaxBrackets] ([Year], [FromAmount], [ToAmount], [Rate], [CreatedAt], [IsDeleted]) VALUES
                    (2026, 0,       40000,   0,    GETUTCDATE(), 0),
                    (2026, 40000,   55000,   10,   GETUTCDATE(), 0),
                    (2026, 55000,   70000,   15,   GETUTCDATE(), 0),
                    (2026, 70000,   200000,  20,   GETUTCDATE(), 0),
                    (2026, 200000,  400000,  22.5, GETUTCDATE(), 0),
                    (2026, 400000,  1200000, 25,   GETUTCDATE(), 0),
                    (2026, 1200000, NULL,    27.5, GETUTCDATE(), 0);

                UPDATE [SystemSettings] SET [Value] = '20000' WHERE [Key] = 'TaxPersonalExemption' AND [Value] = '0';

                IF NOT EXISTS (SELECT 1 FROM [SalaryComponents])
                INSERT INTO [SalaryComponents] ([Name], [Type], [CalcMethod], [IsTaxable], [IsInsurable], [CreatedAt], [IsDeleted]) VALUES
                    (N'بدل انتقال', 1, 1, 1, 0, GETUTCDATE(), 0),
                    (N'حافز شهري', 1, 2, 1, 0, GETUTCDATE(), 0),
                    (N'اشتراك تأمين طبي', 2, 1, 0, 0, GETUTCDATE(), 0);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AbsentDays",
                table: "PayrollItems");

            migrationBuilder.DropColumn(
                name: "LateMinutes",
                table: "PayrollItems");

            migrationBuilder.DropColumn(
                name: "OvertimeMinutes",
                table: "PayrollItems");

            migrationBuilder.DropColumn(
                name: "SalaryDays",
                table: "PayrollItems");
        }
    }
}
