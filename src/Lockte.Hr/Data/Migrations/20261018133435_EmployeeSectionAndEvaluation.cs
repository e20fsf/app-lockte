using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Hr.Data.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeSectionAndEvaluation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "Evaluation1Date",
                schema: "hr",
                table: "Employees",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Evaluation1Grade",
                schema: "hr",
                table: "Employees",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Evaluation1Score",
                schema: "hr",
                table: "Employees",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "Evaluation2Date",
                schema: "hr",
                table: "Employees",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Evaluation2Grade",
                schema: "hr",
                table: "Employees",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Evaluation2Score",
                schema: "hr",
                table: "Employees",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ProbationPassedDate",
                schema: "hr",
                table: "Employees",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SectionId",
                schema: "hr",
                table: "Employees",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_SectionId",
                schema: "hr",
                table: "Employees",
                column: "SectionId");

            migrationBuilder.Sql("""
                ALTER TABLE hr.Employees ADD CONSTRAINT FK_Employees_org_Sections_SectionId
                    FOREIGN KEY (SectionId) REFERENCES org.Sections (Id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE hr.Employees DROP CONSTRAINT FK_Employees_org_Sections_SectionId;");

            migrationBuilder.DropIndex(
                name: "IX_Employees_SectionId",
                schema: "hr",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Evaluation1Date",
                schema: "hr",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Evaluation1Grade",
                schema: "hr",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Evaluation1Score",
                schema: "hr",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Evaluation2Date",
                schema: "hr",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Evaluation2Grade",
                schema: "hr",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Evaluation2Score",
                schema: "hr",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ProbationPassedDate",
                schema: "hr",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "SectionId",
                schema: "hr",
                table: "Employees");
        }
    }
}
