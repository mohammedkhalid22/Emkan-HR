using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRSystem.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddCvAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Requirements",
                table: "JobTitles",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CvAnalyses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    EmployeeDocumentId = table.Column<int>(type: "int", nullable: true),
                    JobTitleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    HadRequirements = table.Column<bool>(type: "bit", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ExperienceYears = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: true),
                    Education = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MatchPercent = table.Column<int>(type: "int", nullable: false),
                    MatchReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Skills = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Strengths = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Gaps = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CvAnalyses", x => x.Id);
                    table.CheckConstraint("CK_CvAnalyses_MatchPercent", "[MatchPercent] BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_CvAnalyses_EmployeeDocuments_EmployeeDocumentId",
                        column: x => x.EmployeeDocumentId,
                        principalTable: "EmployeeDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CvAnalyses_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CvAnalyses_EmployeeDocumentId",
                table: "CvAnalyses",
                column: "EmployeeDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_CvAnalyses_EmployeeId",
                table: "CvAnalyses",
                column: "EmployeeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CvAnalyses");

            migrationBuilder.DropColumn(
                name: "Requirements",
                table: "JobTitles");
        }
    }
}
