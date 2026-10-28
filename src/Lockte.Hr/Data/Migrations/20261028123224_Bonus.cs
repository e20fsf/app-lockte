using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Hr.Data.Migrations
{
    /// <inheritdoc />
    public partial class Bonus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BonusRuns",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    MonthlyMultiplier = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    DailyMultiplier = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BonusRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BonusResults",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BonusRunId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    LeaveDays = table.Column<double>(type: "float", nullable: false),
                    ServiceYears = table.Column<double>(type: "float", nullable: true),
                    Percent = table.Column<double>(type: "float", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BonusResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BonusResults_BonusRuns_BonusRunId",
                        column: x => x.BonusRunId,
                        principalSchema: "hr",
                        principalTable: "BonusRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BonusResults_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "hr",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BonusResults_BonusRunId_EmployeeId",
                schema: "hr",
                table: "BonusResults",
                columns: new[] { "BonusRunId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BonusResults_EmployeeId",
                schema: "hr",
                table: "BonusResults",
                column: "EmployeeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BonusResults",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "BonusRuns",
                schema: "hr");
        }
    }
}
