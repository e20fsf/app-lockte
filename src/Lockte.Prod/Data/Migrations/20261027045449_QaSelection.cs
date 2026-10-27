using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Prod.Data.Migrations
{
    /// <inheritdoc />
    public partial class QaSelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "QaSelected",
                schema: "prod",
                table: "ProductionReportLines",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionReportLines_QaSelected",
                schema: "prod",
                table: "ProductionReportLines",
                column: "QaSelected",
                filter: "[QaSelected] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductionReportLines_QaSelected",
                schema: "prod",
                table: "ProductionReportLines");

            migrationBuilder.DropColumn(
                name: "QaSelected",
                schema: "prod",
                table: "ProductionReportLines");
        }
    }
}
