using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Calib.Data.Migrations
{
    /// <inheritdoc />
    public partial class CalibFixedRecordTexts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PressureGaugeLimit",
                schema: "calib",
                table: "Settings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "±10 kg/cm²");

            migrationBuilder.AddColumn<string>(
                name: "PressureGaugeName",
                schema: "calib",
                table: "Settings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Pressure Gauge");

            migrationBuilder.AddColumn<string>(
                name: "PressureGaugeRange",
                schema: "calib",
                table: "Settings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "50 - 150 Kg/cm²");

            migrationBuilder.AddColumn<string>(
                name: "TempControlLimit",
                schema: "calib",
                table: "Settings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "±10 °C");

            migrationBuilder.AddColumn<string>(
                name: "TempControlName",
                schema: "calib",
                table: "Settings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Temp Control");

            migrationBuilder.AddColumn<string>(
                name: "TempControlRange",
                schema: "calib",
                table: "Settings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "190 - 210 °C");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PressureGaugeLimit",
                schema: "calib",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "PressureGaugeName",
                schema: "calib",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "PressureGaugeRange",
                schema: "calib",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "TempControlLimit",
                schema: "calib",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "TempControlName",
                schema: "calib",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "TempControlRange",
                schema: "calib",
                table: "Settings");
        }
    }
}
