using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Sales.Data.Migrations
{
    /// <inheritdoc />
    public partial class Deliveries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PendingAmount",
                schema: "sales",
                table: "SalesOrderLines",
                type: "decimal(24,8)",
                precision: 24,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PendingBoxes",
                schema: "sales",
                table: "SalesOrderLines",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PendingDeliveryDate",
                schema: "sales",
                table: "SalesOrderLines",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PendingInvoiceLine",
                schema: "sales",
                table: "SalesOrderLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingInvoiceNo",
                schema: "sales",
                table: "SalesOrderLines",
                type: "nvarchar(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingLot",
                schema: "sales",
                table: "SalesOrderLines",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PendingQuantity",
                schema: "sales",
                table: "SalesOrderLines",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Deliveries",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OrderDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LineNo = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SalesTerritoryId = table.Column<int>(type: "int", nullable: true),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    InvoiceNo = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: true),
                    InvoiceLine = table.Column<int>(type: "int", nullable: true),
                    Lot = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(24,8)", precision: 24, scale: 8, nullable: true),
                    Boxes = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Complete = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Deliveries_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "sales",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_CustomerId",
                schema: "sales",
                table: "Deliveries",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_DeliveryDate",
                schema: "sales",
                table: "Deliveries",
                column: "DeliveryDate");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_InvoiceNo",
                schema: "sales",
                table: "Deliveries",
                column: "InvoiceNo");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_OrderNo",
                schema: "sales",
                table: "Deliveries",
                column: "OrderNo");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_ProductId",
                schema: "sales",
                table: "Deliveries",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Deliveries",
                schema: "sales");

            migrationBuilder.DropColumn(
                name: "PendingAmount",
                schema: "sales",
                table: "SalesOrderLines");

            migrationBuilder.DropColumn(
                name: "PendingBoxes",
                schema: "sales",
                table: "SalesOrderLines");

            migrationBuilder.DropColumn(
                name: "PendingDeliveryDate",
                schema: "sales",
                table: "SalesOrderLines");

            migrationBuilder.DropColumn(
                name: "PendingInvoiceLine",
                schema: "sales",
                table: "SalesOrderLines");

            migrationBuilder.DropColumn(
                name: "PendingInvoiceNo",
                schema: "sales",
                table: "SalesOrderLines");

            migrationBuilder.DropColumn(
                name: "PendingLot",
                schema: "sales",
                table: "SalesOrderLines");

            migrationBuilder.DropColumn(
                name: "PendingQuantity",
                schema: "sales",
                table: "SalesOrderLines");
        }
    }
}
