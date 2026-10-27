using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Prod.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoldUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MoldCutoffs",
                schema: "prod",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MoldId = table.Column<int>(type: "int", nullable: false),
                    CutDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Cumulative = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoldCutoffs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MoldCutoffs_Molds_MoldId",
                        column: x => x.MoldId,
                        principalSchema: "prod",
                        principalTable: "Molds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MoldUsages",
                schema: "prod",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MoldId = table.Column<int>(type: "int", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Shots = table.Column<int>(type: "int", nullable: false),
                    IsOpening = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoldUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MoldUsages_Molds_MoldId",
                        column: x => x.MoldId,
                        principalSchema: "prod",
                        principalTable: "Molds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MoldCutoffs_CutDate_MoldId",
                schema: "prod",
                table: "MoldCutoffs",
                columns: new[] { "CutDate", "MoldId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MoldCutoffs_MoldId",
                schema: "prod",
                table: "MoldCutoffs",
                column: "MoldId");

            migrationBuilder.CreateIndex(
                name: "IX_MoldUsages_Day",
                schema: "prod",
                table: "MoldUsages",
                column: "Day");

            migrationBuilder.CreateIndex(
                name: "IX_MoldUsages_MoldId_Day",
                schema: "prod",
                table: "MoldUsages",
                columns: new[] { "MoldId", "Day" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MoldCutoffs",
                schema: "prod");

            migrationBuilder.DropTable(
                name: "MoldUsages",
                schema: "prod");
        }
    }
}
