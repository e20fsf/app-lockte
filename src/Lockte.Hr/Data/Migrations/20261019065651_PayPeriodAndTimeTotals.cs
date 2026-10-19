using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Hr.Data.Migrations
{
    /// <inheritdoc />
    public partial class PayPeriodAndTimeTotals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayPeriods",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PayDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CompletedStep = table.Column<int>(type: "int", nullable: false),
                    TimeCalculatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TimeCalculatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayPeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayPeriodTimeTotals",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayPeriodId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Normal = table.Column<double>(type: "float", nullable: false),
                    PaidLeave = table.Column<double>(type: "float", nullable: false),
                    UnpaidLeave = table.Column<double>(type: "float", nullable: false),
                    Overtime = table.Column<double>(type: "float", nullable: false),
                    Sunday = table.Column<double>(type: "float", nullable: false),
                    SundayOvertime = table.Column<double>(type: "float", nullable: false),
                    Holiday = table.Column<double>(type: "float", nullable: false),
                    HolidayOvertime = table.Column<double>(type: "float", nullable: false),
                    PublicHolidays = table.Column<int>(type: "int", nullable: false),
                    WeeklyHolidays = table.Column<int>(type: "int", nullable: false),
                    WeeklyHolidaysToResign = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayPeriodTimeTotals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayPeriodTimeTotals_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "hr",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayPeriodTimeTotals_PayPeriods_PayPeriodId",
                        column: x => x.PayPeriodId,
                        principalSchema: "hr",
                        principalTable: "PayPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayPeriods_FromDate",
                schema: "hr",
                table: "PayPeriods",
                column: "FromDate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayPeriodTimeTotals_EmployeeId",
                schema: "hr",
                table: "PayPeriodTimeTotals",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayPeriodTimeTotals_PayPeriodId_EmployeeId",
                schema: "hr",
                table: "PayPeriodTimeTotals",
                columns: new[] { "PayPeriodId", "EmployeeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayPeriodTimeTotals",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "PayPeriods",
                schema: "hr");
        }
    }
}
