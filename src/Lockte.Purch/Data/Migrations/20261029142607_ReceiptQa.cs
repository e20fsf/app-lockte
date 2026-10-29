using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Purch.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReceiptQa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasCertificate",
                schema: "purch",
                table: "GoodsReceipts",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Lot",
                schema: "purch",
                table: "GoodsReceipts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QaNote",
                schema: "purch",
                table: "GoodsReceipts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasCertificate",
                schema: "purch",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "Lot",
                schema: "purch",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "QaNote",
                schema: "purch",
                table: "GoodsReceipts");
        }
    }
}
