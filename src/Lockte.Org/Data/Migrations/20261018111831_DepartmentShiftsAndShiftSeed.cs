using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Org.Data.Migrations
{
    /// <inheritdoc />
    public partial class DepartmentShiftsAndShiftSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DepartmentShifts",
                schema: "org",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    ShiftId = table.Column<int>(type: "int", nullable: false),
                    SupervisorEmployeeId = table.Column<int>(type: "int", nullable: true),
                    SupervisorName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ManagerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FactoryManagerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentShifts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepartmentShifts_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "org",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepartmentShifts_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalSchema: "org",
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentShifts_DepartmentId_ShiftId",
                schema: "org",
                table: "DepartmentShifts",
                columns: new[] { "DepartmentId", "ShiftId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentShifts_ShiftId",
                schema: "org",
                table: "DepartmentShifts",
                column: "ShiftId");

            // Keep what is there: each department × shift row, with the department's printed names copied onto it.
            migrationBuilder.Sql("""
                INSERT INTO org.DepartmentShifts
                    (DepartmentId, ShiftId, SupervisorEmployeeId, ManagerName, FactoryManagerName, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt)
                SELECT h.DepartmentId, h.ShiftId, h.EmployeeId, d.ManagerName, d.FactoryManagerName,
                       h.CreatedBy, h.CreatedAt, h.UpdatedBy, h.UpdatedAt
                FROM org.ShiftSupervisors h JOIN org.Departments d ON d.Id = h.DepartmentId;
                """);

            migrationBuilder.DropTable(
                name: "ShiftSupervisors",
                schema: "org");

            migrationBuilder.DropColumn(
                name: "FactoryManagerName",
                schema: "org",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "ManagerName",
                schema: "org",
                table: "Departments");

            // กะ A–G with the standard times of T-SHIFT (specs/employee/data-model.md); codes already there are kept.
            migrationBuilder.Sql("""
                INSERT INTO org.Shifts (Code, WorkIn, WorkOut, BreakStart, BreakEnd, OvertimeIn, OvertimeOut, RestMinutes,
                                        CreatedBy, CreatedAt, UpdatedBy, UpdatedAt)
                SELECT v.Code, v.WorkIn, v.WorkOut, v.BreakStart, v.BreakEnd, v.OvertimeIn, v.OvertimeOut, 60,
                       'system', SYSDATETIMEOFFSET(), 'system', SYSDATETIMEOFFSET()
                FROM (VALUES
                    ('A', '08:00', '17:00', '12:00', '13:00', '17:20', '20:10'),
                    ('B', '20:10', '05:10', '00:10', '01:10', '05:30', '08:10'),
                    ('C', '08:00', '17:00', '12:00', '13:00', '17:20', '20:10'),
                    ('D', '08:00', '17:00', '12:00', '13:00', '17:20', '20:10'),
                    ('E', '08:00', '17:00', '12:00', '13:00', NULL, NULL),
                    ('F', '17:00', '02:00', '21:00', '22:00', NULL, NULL),
                    ('G', '08:00', '17:00', '12:00', '13:00', '17:20', '20:10')
                ) AS v (Code, WorkIn, WorkOut, BreakStart, BreakEnd, OvertimeIn, OvertimeOut)
                WHERE NOT EXISTS (SELECT 1 FROM org.Shifts s WHERE s.Code = v.Code);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FactoryManagerName",
                schema: "org",
                table: "Departments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManagerName",
                schema: "org",
                table: "Departments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ShiftSupervisors",
                schema: "org",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    ShiftId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftSupervisors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftSupervisors_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "org",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShiftSupervisors_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalSchema: "org",
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSupervisors_DepartmentId_ShiftId",
                schema: "org",
                table: "ShiftSupervisors",
                columns: new[] { "DepartmentId", "ShiftId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSupervisors_ShiftId",
                schema: "org",
                table: "ShiftSupervisors",
                column: "ShiftId");


            migrationBuilder.Sql("""
                INSERT INTO org.ShiftSupervisors (DepartmentId, ShiftId, EmployeeId, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt)
                SELECT DepartmentId, ShiftId, SupervisorEmployeeId, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt
                FROM org.DepartmentShifts;
                UPDATE d SET ManagerName = x.ManagerName, FactoryManagerName = x.FactoryManagerName
                FROM org.Departments d
                CROSS APPLY (SELECT TOP 1 ManagerName, FactoryManagerName FROM org.DepartmentShifts h
                             WHERE h.DepartmentId = d.Id ORDER BY h.ShiftId) x;
                """);

            migrationBuilder.DropTable(
                name: "DepartmentShifts",
                schema: "org");
        }
    }
}
