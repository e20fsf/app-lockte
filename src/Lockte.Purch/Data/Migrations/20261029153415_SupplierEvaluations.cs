using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Purch.Data.Migrations
{
    /// <inheritdoc />
    public partial class SupplierEvaluations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupplierEvaluations",
                schema: "purch",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Quarter = table.Column<int>(type: "int", nullable: false),
                    Passed = table.Column<int>(type: "int", nullable: false),
                    Failed = table.Column<int>(type: "int", nullable: false),
                    WithCertificate = table.Column<int>(type: "int", nullable: false),
                    WithoutCertificate = table.Column<int>(type: "int", nullable: false),
                    OnTime = table.Column<int>(type: "int", nullable: false),
                    Late = table.Column<int>(type: "int", nullable: false),
                    Point1 = table.Column<int>(type: "int", nullable: false),
                    Point2 = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierEvaluations_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "purch",
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierEvaluations_Quarter_FromDate",
                schema: "purch",
                table: "SupplierEvaluations",
                columns: new[] { "Quarter", "FromDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierEvaluations_SupplierId_FromDate_ToDate",
                schema: "purch",
                table: "SupplierEvaluations",
                columns: new[] { "SupplierId", "FromDate", "ToDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierEvaluations",
                schema: "purch");
        }
    }
}
