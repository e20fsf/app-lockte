using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Prod.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProductionReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductionReports",
                schema: "prod",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentCode = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    ReportDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReportNo = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    MachineId = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductionReportLines",
                schema: "prod",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionReportId = table.Column<int>(type: "int", nullable: false),
                    LineNo = table.Column<int>(type: "int", nullable: false),
                    ShiftId = table.Column<int>(type: "int", nullable: true),
                    Hours = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    MaterialLot1 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MaterialLot2 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProductId = table.Column<int>(type: "int", nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    GoodQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RejectQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    TotalQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: true),
                    MoldId = table.Column<int>(type: "int", nullable: true),
                    Lot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionReportLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionReportLines_Molds_MoldId",
                        column: x => x.MoldId,
                        principalSchema: "prod",
                        principalTable: "Molds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionReportLines_ProductionReports_ProductionReportId",
                        column: x => x.ProductionReportId,
                        principalSchema: "prod",
                        principalTable: "ProductionReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductionReportLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "prod",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionReportLines_EmployeeId",
                schema: "prod",
                table: "ProductionReportLines",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionReportLines_MoldId",
                schema: "prod",
                table: "ProductionReportLines",
                column: "MoldId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionReportLines_ProductId",
                schema: "prod",
                table: "ProductionReportLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionReportLines_ProductionReportId_LineNo",
                schema: "prod",
                table: "ProductionReportLines",
                columns: new[] { "ProductionReportId", "LineNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionReports_DepartmentCode_ReportDate",
                schema: "prod",
                table: "ProductionReports",
                columns: new[] { "DepartmentCode", "ReportDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionReports_ReportNo",
                schema: "prod",
                table: "ProductionReports",
                column: "ReportNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductionReportLines",
                schema: "prod");

            migrationBuilder.DropTable(
                name: "ProductionReports",
                schema: "prod");
        }
    }
}
