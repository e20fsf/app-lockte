using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Hr.Data.Migrations
{
    /// <inheritdoc />
    public partial class PayResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PayCalculatedAt",
                schema: "hr",
                table: "PayPeriods",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayCalculatedBy",
                schema: "hr",
                table: "PayPeriods",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PayResults",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayPeriodId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    EmployeeType = table.Column<int>(type: "int", nullable: false),
                    Rate = table.Column<double>(type: "float", nullable: false),
                    Normal = table.Column<double>(type: "float", nullable: false),
                    PaidLeave = table.Column<double>(type: "float", nullable: false),
                    Overtime = table.Column<double>(type: "float", nullable: false),
                    HolidayHours = table.Column<int>(type: "int", nullable: false),
                    PayNormal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PayOvertime = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Hot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PublicHolidays = table.Column<int>(type: "int", nullable: false),
                    WeeklyHolidays = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayResults_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "hr",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayResults_PayPeriods_PayPeriodId",
                        column: x => x.PayPeriodId,
                        principalSchema: "hr",
                        principalTable: "PayPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayResults_EmployeeId",
                schema: "hr",
                table: "PayResults",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayResults_PayPeriodId_EmployeeId",
                schema: "hr",
                table: "PayResults",
                columns: new[] { "PayPeriodId", "EmployeeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayResults",
                schema: "hr");

            migrationBuilder.DropColumn(
                name: "PayCalculatedAt",
                schema: "hr",
                table: "PayPeriods");

            migrationBuilder.DropColumn(
                name: "PayCalculatedBy",
                schema: "hr",
                table: "PayPeriods");
        }
    }
}
