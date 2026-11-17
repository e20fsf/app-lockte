using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Calib.Data.Migrations
{
    /// <inheritdoc />
    public partial class CalibManualNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ManualCaliper",
                schema: "calib",
                table: "Settings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ManualPressureGauge",
                schema: "calib",
                table: "Settings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ManualScale",
                schema: "calib",
                table: "Settings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ManualTempControl",
                schema: "calib",
                table: "Settings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManualCaliper",
                schema: "calib",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "ManualPressureGauge",
                schema: "calib",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "ManualScale",
                schema: "calib",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "ManualTempControl",
                schema: "calib",
                table: "Settings");
        }
    }
}
