using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Sales.Data.Migrations
{
    /// <inheritdoc />
    public partial class CustomerDetailsAndTerritories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                schema: "sales",
                table: "Customers",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreditDays",
                schema: "sales",
                table: "Customers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "District",
                schema: "sales",
                table: "Customers",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone1",
                schema: "sales",
                table: "Customers",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone2",
                schema: "sales",
                table: "Customers",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                schema: "sales",
                table: "Customers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Province",
                schema: "sales",
                table: "Customers",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remark",
                schema: "sales",
                table: "Customers",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SalesTerritoryId",
                schema: "sales",
                table: "Customers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransportAddress",
                schema: "sales",
                table: "Customers",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransportName",
                schema: "sales",
                table: "Customers",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransportPhone",
                schema: "sales",
                table: "Customers",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SalesTerritories",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesTerritories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_PostalCode",
                schema: "sales",
                table: "Customers",
                column: "PostalCode");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_SalesTerritoryId",
                schema: "sales",
                table: "Customers",
                column: "SalesTerritoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesTerritories_Code",
                schema: "sales",
                table: "SalesTerritories",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_SalesTerritories_SalesTerritoryId",
                schema: "sales",
                table: "Customers",
                column: "SalesTerritoryId",
                principalSchema: "sales",
                principalTable: "SalesTerritories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_SalesTerritories_SalesTerritoryId",
                schema: "sales",
                table: "Customers");

            migrationBuilder.DropTable(
                name: "SalesTerritories",
                schema: "sales");

            migrationBuilder.DropIndex(
                name: "IX_Customers_PostalCode",
                schema: "sales",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_SalesTerritoryId",
                schema: "sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Address",
                schema: "sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CreditDays",
                schema: "sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "District",
                schema: "sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Phone1",
                schema: "sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Phone2",
                schema: "sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                schema: "sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Province",
                schema: "sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Remark",
                schema: "sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "SalesTerritoryId",
                schema: "sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "TransportAddress",
                schema: "sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "TransportName",
                schema: "sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "TransportPhone",
                schema: "sales",
                table: "Customers");
        }
    }
}
