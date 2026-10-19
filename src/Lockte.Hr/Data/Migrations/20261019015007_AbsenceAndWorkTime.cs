using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Hr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AbsenceAndWorkTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Absences",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    WorkIn = table.Column<TimeOnly>(type: "time", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Absences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Absences_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "hr",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkTimes",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    WorkIn = table.Column<TimeOnly>(type: "time", nullable: true),
                    WorkOut = table.Column<TimeOnly>(type: "time", nullable: true),
                    Late = table.Column<TimeOnly>(type: "time", nullable: true),
                    RestMinutes = table.Column<int>(type: "int", nullable: false),
                    Mark = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkTimes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkTimes_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "hr",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Absences_Date_EmployeeId",
                schema: "hr",
                table: "Absences",
                columns: new[] { "Date", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_Absences_EmployeeId",
                schema: "hr",
                table: "Absences",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTimes_Date_EmployeeId",
                schema: "hr",
                table: "WorkTimes",
                columns: new[] { "Date", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkTimes_EmployeeId_Date",
                schema: "hr",
                table: "WorkTimes",
                columns: new[] { "EmployeeId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Absences",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "WorkTimes",
                schema: "hr");
        }
    }
}
