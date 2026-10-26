using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Prod.Data.Migrations
{
    /// <inheritdoc />
    public partial class StockLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StockedAt",
                schema: "prod",
                table: "ProductionReports",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StockEntries",
                schema: "prod",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: true),
                    DocumentNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LineNo = table.Column<int>(type: "int", nullable: true),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: true),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClosedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockEntries_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "prod",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockEntries_DocumentNo",
                schema: "prod",
                table: "StockEntries",
                column: "DocumentNo");

            migrationBuilder.CreateIndex(
                name: "IX_StockEntries_Kind_Date",
                schema: "prod",
                table: "StockEntries",
                columns: new[] { "Kind", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_StockEntries_ProductId_ClosedOn",
                schema: "prod",
                table: "StockEntries",
                columns: new[] { "ProductId", "ClosedOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockEntries",
                schema: "prod");

            migrationBuilder.DropColumn(
                name: "StockedAt",
                schema: "prod",
                table: "ProductionReports");
        }
    }
}
