using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Cash.Data.Migrations
{
    /// <inheritdoc />
    public partial class ScrapRemittances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RemittanceId",
                schema: "cash",
                table: "ScrapReceipts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ScrapRemittances",
                schema: "cash",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MadeOn = table.Column<DateOnly>(type: "date", nullable: false),
                    FromNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ToNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    MadeBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrapRemittances", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScrapReceipts_RemittanceId",
                schema: "cash",
                table: "ScrapReceipts",
                column: "RemittanceId");

            migrationBuilder.AddForeignKey(
                name: "FK_ScrapReceipts_ScrapRemittances_RemittanceId",
                schema: "cash",
                table: "ScrapReceipts",
                column: "RemittanceId",
                principalSchema: "cash",
                principalTable: "ScrapRemittances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScrapReceipts_ScrapRemittances_RemittanceId",
                schema: "cash",
                table: "ScrapReceipts");

            migrationBuilder.DropTable(
                name: "ScrapRemittances",
                schema: "cash");

            migrationBuilder.DropIndex(
                name: "IX_ScrapReceipts_RemittanceId",
                schema: "cash",
                table: "ScrapReceipts");

            migrationBuilder.DropColumn(
                name: "RemittanceId",
                schema: "cash",
                table: "ScrapReceipts");
        }
    }
}
