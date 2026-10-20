using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Hr.Data.Migrations
{
    /// <inheritdoc />
    public partial class SlipPartsAndSigners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "HolidayOvertimePay",
                schema: "hr",
                table: "PayResults",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HolidayPay",
                schema: "hr",
                table: "PayResults",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OvertimePay",
                schema: "hr",
                table: "PayResults",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SundayOvertimePay",
                schema: "hr",
                table: "PayResults",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SundayPay",
                schema: "hr",
                table: "PayResults",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "SlipSigners",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Role = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Signature = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    SignatureContentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlipSigners", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SlipSigners_Role",
                schema: "hr",
                table: "SlipSigners",
                column: "Role",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SlipSigners",
                schema: "hr");

            migrationBuilder.DropColumn(
                name: "HolidayOvertimePay",
                schema: "hr",
                table: "PayResults");

            migrationBuilder.DropColumn(
                name: "HolidayPay",
                schema: "hr",
                table: "PayResults");

            migrationBuilder.DropColumn(
                name: "OvertimePay",
                schema: "hr",
                table: "PayResults");

            migrationBuilder.DropColumn(
                name: "SundayOvertimePay",
                schema: "hr",
                table: "PayResults");

            migrationBuilder.DropColumn(
                name: "SundayPay",
                schema: "hr",
                table: "PayResults");
        }
    }
}
