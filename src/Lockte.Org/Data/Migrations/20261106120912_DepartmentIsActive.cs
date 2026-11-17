using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Org.Data.Migrations
{
    /// <inheritdoc />
    public partial class DepartmentIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "org",
                table: "Departments",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "org",
                table: "Departments");
        }
    }
}
