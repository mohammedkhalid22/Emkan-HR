using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRSystem.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaveTypeSeniorDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SeniorAnnualDays",
                table: "LeaveTypes",
                type: "int",
                nullable: true);

            // The seeded annual leave type: 30 days for 10+ years of service or age 50+
            migrationBuilder.Sql("UPDATE [LeaveTypes] SET [SeniorAnnualDays] = 30 WHERE [Name] = N'سنوية'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SeniorAnnualDays",
                table: "LeaveTypes");
        }
    }
}
