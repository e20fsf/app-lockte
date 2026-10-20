using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Hr.Data.Migrations
{
    /// <inheritdoc />
    public partial class Warnings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Warnings",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    Level = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warnings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Warnings_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "hr",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Warnings_Date",
                schema: "hr",
                table: "Warnings",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Warnings_DepartmentId",
                schema: "hr",
                table: "Warnings",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Warnings_EmployeeId_Date",
                schema: "hr",
                table: "Warnings",
                columns: new[] { "EmployeeId", "Date" });

            // แผนกตอนบันทึก lives in module องค์กร (ADR 0002: foreign keys across schemas are allowed; HrDbContext does not map org).
            migrationBuilder.Sql("""
                ALTER TABLE hr.Warnings ADD CONSTRAINT FK_Warnings_org_Departments_DepartmentId
                    FOREIGN KEY (DepartmentId) REFERENCES org.Departments (Id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Warnings",
                schema: "hr");
        }
    }
}
