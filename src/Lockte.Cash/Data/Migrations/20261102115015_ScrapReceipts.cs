using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Cash.Data.Migrations
{
    /// <inheritdoc />
    public partial class ScrapReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScrapReceipts",
                schema: "cash",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    No = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    BuyerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BuyerAddress = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    BuyerTel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ScrapBuyerId = table.Column<int>(type: "int", nullable: true),
                    IsCancelled = table.Column<bool>(type: "bit", nullable: false),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CancelledBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrapReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScrapReceipts_ScrapBuyers_ScrapBuyerId",
                        column: x => x.ScrapBuyerId,
                        principalSchema: "cash",
                        principalTable: "ScrapBuyers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ScrapReceiptLines",
                schema: "cash",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReceiptId = table.Column<int>(type: "int", nullable: false),
                    Seq = table.Column<int>(type: "int", nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ScrapItemId = table.Column<int>(type: "int", nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Qty = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrapReceiptLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScrapReceiptLines_ScrapItems_ScrapItemId",
                        column: x => x.ScrapItemId,
                        principalSchema: "cash",
                        principalTable: "ScrapItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ScrapReceiptLines_ScrapReceipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "cash",
                        principalTable: "ScrapReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScrapReceiptLines_ReceiptId_Seq",
                schema: "cash",
                table: "ScrapReceiptLines",
                columns: new[] { "ReceiptId", "Seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScrapReceiptLines_ScrapItemId",
                schema: "cash",
                table: "ScrapReceiptLines",
                column: "ScrapItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ScrapReceipts_Date",
                schema: "cash",
                table: "ScrapReceipts",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_ScrapReceipts_No",
                schema: "cash",
                table: "ScrapReceipts",
                column: "No",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScrapReceipts_ScrapBuyerId",
                schema: "cash",
                table: "ScrapReceipts",
                column: "ScrapBuyerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScrapReceiptLines",
                schema: "cash");

            migrationBuilder.DropTable(
                name: "ScrapReceipts",
                schema: "cash");
        }
    }
}
