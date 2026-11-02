using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Calib.Data.Migrations
{
    /// <inheritdoc />
    public partial class CalibPlanSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalRegisterNote",
                schema: "calib",
                table: "Settings",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "เครื่องทดสอบแรงเสียดทาน สอบเทียบที่โรงงาน มีรายละเอียดดังนี้ 1.ความเร็วรอบมอเตอร์ 500 rpm 2.ตัวควบคุมอุณหภูมิ (temp control) 100,150,200,250 3. แรงกด 125 Kgs.");

            migrationBuilder.AddColumn<int>(
                name: "PlanExternalMonth",
                schema: "calib",
                table: "Settings",
                type: "int",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.AddColumn<int>(
                name: "PlanInternalMonth",
                schema: "calib",
                table: "Settings",
                type: "int",
                nullable: false,
                defaultValue: 8);

            migrationBuilder.AddColumn<string>(
                name: "PlanNote",
                schema: "calib",
                table: "Settings",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "เครื่องทดสอบแรงเสียดทาน สอบเทียบที่โรงงาน มีรายละเอียดดังนี้ 1.ความเร็วรอบมอเตอร์ 500 rpm 2.ตัวควบคุมอุณหภูมิ (temp control) 100,150,200,250,300,350 3. แรงกด 125 Kgs.");

            migrationBuilder.AddColumn<string>(
                name: "PlanResponsible",
                schema: "calib",
                table: "Settings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "QA");

            migrationBuilder.AddColumn<string>(
                name: "PlanReviewer",
                schema: "calib",
                table: "Settings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "QMR");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalRegisterNote",
                schema: "calib",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "PlanExternalMonth",
                schema: "calib",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "PlanInternalMonth",
                schema: "calib",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "PlanNote",
                schema: "calib",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "PlanResponsible",
                schema: "calib",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "PlanReviewer",
                schema: "calib",
                table: "Settings");
        }
    }
}
