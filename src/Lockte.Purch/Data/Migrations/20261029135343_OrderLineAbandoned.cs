using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Purch.Data.Migrations
{
    /// <inheritdoc />
    public partial class OrderLineAbandoned : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AbandonedQuantity",
                schema: "purch",
                table: "PurchaseRequestLines",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AbandonedQuantity",
                schema: "purch",
                table: "PurchaseRequestLines");
        }
    }
}
