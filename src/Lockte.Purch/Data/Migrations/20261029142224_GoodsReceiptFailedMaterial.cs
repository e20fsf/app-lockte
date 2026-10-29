using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Purch.Data.Migrations
{
    /// <inheritdoc />
    public partial class GoodsReceiptFailedMaterial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Failed",
                schema: "purch",
                table: "GoodsReceipts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaterialId",
                schema: "purch",
                table: "GoodsReceipts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MaterialName",
                schema: "purch",
                table: "GoodsReceipts",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                schema: "purch",
                table: "GoodsReceipts",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_MaterialId",
                schema: "purch",
                table: "GoodsReceipts",
                column: "MaterialId");

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceipts_Materials_MaterialId",
                schema: "purch",
                table: "GoodsReceipts",
                column: "MaterialId",
                principalSchema: "purch",
                principalTable: "Materials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GoodsReceipts_Materials_MaterialId",
                schema: "purch",
                table: "GoodsReceipts");

            migrationBuilder.DropIndex(
                name: "IX_GoodsReceipts_MaterialId",
                schema: "purch",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "Failed",
                schema: "purch",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "MaterialId",
                schema: "purch",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "MaterialName",
                schema: "purch",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "Unit",
                schema: "purch",
                table: "GoodsReceipts");
        }
    }
}
