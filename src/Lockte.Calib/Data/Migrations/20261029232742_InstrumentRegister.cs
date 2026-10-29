using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Calib.Data.Migrations
{
    /// <inheritdoc />
    public partial class InstrumentRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Instruments",
                schema: "calib",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RegisteredOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Maker = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    Range = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Resolution = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Limit = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StandardUsed = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CalibratorEmployeeId = table.Column<int>(type: "int", nullable: true),
                    Axis = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ManualNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Instruments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InstrumentMachines",
                schema: "calib",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstrumentId = table.Column<int>(type: "int", nullable: false),
                    MachineId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstrumentMachines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstrumentMachines_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalSchema: "calib",
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InstrumentMachines_InstrumentId_MachineId",
                schema: "calib",
                table: "InstrumentMachines",
                columns: new[] { "InstrumentId", "MachineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Instruments_Code",
                schema: "calib",
                table: "Instruments",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Instruments_Kind",
                schema: "calib",
                table: "Instruments",
                column: "Kind");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InstrumentMachines",
                schema: "calib");

            migrationBuilder.DropTable(
                name: "Instruments",
                schema: "calib");
        }
    }
}
