using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Org.Data.Migrations
{
    /// <inheritdoc />
    public partial class DepartmentStaffingTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StaffingTarget",
                schema: "org",
                table: "Departments",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StaffingTarget",
                schema: "org",
                table: "Departments");
        }
    }
}
