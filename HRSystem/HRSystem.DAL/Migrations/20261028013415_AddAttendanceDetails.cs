using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRSystem.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AttendanceRequests_EmployeeId",
                table: "AttendanceRequests");

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "AttendanceRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EarlyLeaveMinutes",
                table: "AttendanceRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "AttendanceRecords",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_Name",
                table: "Shifts",
                column: "Name",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Shifts_Times",
                table: "Shifts",
                sql: "[StartTime] < [EndTime]");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRequests_EmployeeId_Date",
                table: "AttendanceRequests",
                columns: new[] { "EmployeeId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Shifts_Name",
                table: "Shifts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Shifts_Times",
                table: "Shifts");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRequests_EmployeeId_Date",
                table: "AttendanceRequests");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "AttendanceRequests");

            migrationBuilder.DropColumn(
                name: "EarlyLeaveMinutes",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "AttendanceRecords");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRequests_EmployeeId",
                table: "AttendanceRequests",
                column: "EmployeeId");
        }
    }
}
