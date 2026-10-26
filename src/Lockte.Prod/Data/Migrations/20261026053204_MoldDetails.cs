using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Prod.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoldDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AluminumWeight",
                schema: "prod",
                table: "Molds",
                type: "decimal(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Cost",
                schema: "prod",
                table: "Molds",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                schema: "prod",
                table: "Molds",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EndDate",
                schema: "prod",
                table: "Molds",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaintenancePieces",
                schema: "prod",
                table: "Molds",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaintenanceShots",
                schema: "prod",
                table: "Molds",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaintenanceYears",
                schema: "prod",
                table: "Molds",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Maker",
                schema: "prod",
                table: "Molds",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Owner",
                schema: "prod",
                table: "Molds",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PiecesPerShot",
                schema: "prod",
                table: "Molds",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartDate",
                schema: "prod",
                table: "Molds",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Molds_DepartmentId",
                schema: "prod",
                table: "Molds",
                column: "DepartmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Molds_DepartmentId",
                schema: "prod",
                table: "Molds");

            migrationBuilder.DropColumn(
                name: "AluminumWeight",
                schema: "prod",
                table: "Molds");

            migrationBuilder.DropColumn(
                name: "Cost",
                schema: "prod",
                table: "Molds");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                schema: "prod",
                table: "Molds");

            migrationBuilder.DropColumn(
                name: "EndDate",
                schema: "prod",
                table: "Molds");

            migrationBuilder.DropColumn(
                name: "MaintenancePieces",
                schema: "prod",
                table: "Molds");

            migrationBuilder.DropColumn(
                name: "MaintenanceShots",
                schema: "prod",
                table: "Molds");

            migrationBuilder.DropColumn(
                name: "MaintenanceYears",
                schema: "prod",
                table: "Molds");

            migrationBuilder.DropColumn(
                name: "Maker",
                schema: "prod",
                table: "Molds");

            migrationBuilder.DropColumn(
                name: "Owner",
                schema: "prod",
                table: "Molds");

            migrationBuilder.DropColumn(
                name: "PiecesPerShot",
                schema: "prod",
                table: "Molds");

            migrationBuilder.DropColumn(
                name: "StartDate",
                schema: "prod",
                table: "Molds");
        }
    }
}
