using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Hr.Data.Migrations
{
    /// <inheritdoc />
    public partial class LeaveEntitlementsAndMoved : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Moved",
                schema: "hr",
                table: "Warnings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Moved",
                schema: "hr",
                table: "Leaves",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "LeaveEntitlements",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PersonalFirstYear = table.Column<double>(type: "float", nullable: false),
                    Personal = table.Column<double>(type: "float", nullable: false),
                    Sick = table.Column<double>(type: "float", nullable: false),
                    Vacation = table.Column<double>(type: "float", nullable: false),
                    Maternity = table.Column<double>(type: "float", nullable: false),
                    Military = table.Column<double>(type: "float", nullable: false),
                    SpecialMaternity = table.Column<double>(type: "float", nullable: false),
                    SpouseMaternity = table.Column<double>(type: "float", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveEntitlements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveEntitlements_StartDate",
                schema: "hr",
                table: "LeaveEntitlements",
                column: "StartDate",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaveEntitlements",
                schema: "hr");

            migrationBuilder.DropColumn(
                name: "Moved",
                schema: "hr",
                table: "Warnings");

            migrationBuilder.DropColumn(
                name: "Moved",
                schema: "hr",
                table: "Leaves");
        }
    }
}
