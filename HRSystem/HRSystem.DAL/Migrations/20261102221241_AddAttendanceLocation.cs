using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRSystem.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowRemoteAttendance",
                table: "Employees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AttendanceRadiusMeters",
                table: "Branches",
                type: "int",
                nullable: false,
                defaultValue: 200);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Branches",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Branches",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CheckInDistanceMeters",
                table: "AttendanceRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckInLatitude",
                table: "AttendanceRecords",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckInLongitude",
                table: "AttendanceRecords",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CheckOutDistanceMeters",
                table: "AttendanceRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckOutLatitude",
                table: "AttendanceRecords",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckOutLongitude",
                table: "AttendanceRecords",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowRemoteAttendance",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "AttendanceRadiusMeters",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "CheckInDistanceMeters",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "CheckInLatitude",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "CheckInLongitude",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "CheckOutDistanceMeters",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "CheckOutLatitude",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "CheckOutLongitude",
                table: "AttendanceRecords");
        }
    }
}
