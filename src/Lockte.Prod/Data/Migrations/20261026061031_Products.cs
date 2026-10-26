using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Prod.Data.Migrations
{
    /// <inheritdoc />
    public partial class Products : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BrakeTypes",
                schema: "prod",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrakeTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductGroups",
                schema: "prod",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pattern = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductModels",
                schema: "prod",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductModels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductTypes",
                schema: "prod",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                schema: "prod",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Pack = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    AluminumWeight = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    BrakePadWeight = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    CorkWeight = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    SteelCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    PairWeight = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    SellingPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    UnitCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    ProductTypeId = table.Column<int>(type: "int", nullable: true),
                    MoldId = table.Column<int>(type: "int", nullable: true),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    ProducedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ModelId = table.Column<int>(type: "int", nullable: true),
                    Barcode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    StandardNumber = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    BrakeTypeId = table.Column<int>(type: "int", nullable: true),
                    Size = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Formula = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    StandardModel = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_BrakeTypes_BrakeTypeId",
                        column: x => x.BrakeTypeId,
                        principalSchema: "prod",
                        principalTable: "BrakeTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Products_Molds_MoldId",
                        column: x => x.MoldId,
                        principalSchema: "prod",
                        principalTable: "Molds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Products_ProductModels_ModelId",
                        column: x => x.ModelId,
                        principalSchema: "prod",
                        principalTable: "ProductModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Products_ProductTypes_ProductTypeId",
                        column: x => x.ProductTypeId,
                        principalSchema: "prod",
                        principalTable: "ProductTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BrakeTypes_Code",
                schema: "prod",
                table: "BrakeTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BrakeTypes_Name",
                schema: "prod",
                table: "BrakeTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductGroups_Pattern",
                schema: "prod",
                table: "ProductGroups",
                column: "Pattern",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductModels_Name",
                schema: "prod",
                table: "ProductModels",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Barcode",
                schema: "prod",
                table: "Products",
                column: "Barcode");

            migrationBuilder.CreateIndex(
                name: "IX_Products_BrakeTypeId",
                schema: "prod",
                table: "Products",
                column: "BrakeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Code",
                schema: "prod",
                table: "Products",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_CustomerId",
                schema: "prod",
                table: "Products",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_ModelId",
                schema: "prod",
                table: "Products",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_MoldId",
                schema: "prod",
                table: "Products",
                column: "MoldId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_ProductTypeId",
                schema: "prod",
                table: "Products",
                column: "ProductTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductTypes_Name",
                schema: "prod",
                table: "ProductTypes",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductGroups",
                schema: "prod");

            migrationBuilder.DropTable(
                name: "Products",
                schema: "prod");

            migrationBuilder.DropTable(
                name: "BrakeTypes",
                schema: "prod");

            migrationBuilder.DropTable(
                name: "ProductModels",
                schema: "prod");

            migrationBuilder.DropTable(
                name: "ProductTypes",
                schema: "prod");
        }
    }
}
