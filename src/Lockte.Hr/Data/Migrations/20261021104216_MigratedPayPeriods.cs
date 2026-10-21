using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Hr.Data.Migrations
{
    /// <inheritdoc />
    public partial class MigratedPayPeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Migrated",
                schema: "hr",
                table: "PayPeriods",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Periods imported before this column (#51): closed at a pay step with ค่าแรง never calculated here.
            migrationBuilder.Sql("UPDATE hr.PayPeriods SET Migrated = 1 WHERE CompletedStep >= 2 AND PayCalculatedAt IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Migrated",
                schema: "hr",
                table: "PayPeriods");
        }
    }
}
