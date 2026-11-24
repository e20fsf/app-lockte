using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Web.Accounts.Migrations
{
    /// <inheritdoc />
    public partial class UserPrograms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ADR 0005: every non-admin user that exists now (the test users user1 / user2) keeps all five programs, in
            // chooser order (ProgramAccess.All); users added afterwards get what the admin or seed-users gives them.
            migrationBuilder.AddColumn<string>(
                name: "Programs",
                schema: "auth",
                table: "AspNetUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE [auth].[AspNetUsers] SET [Programs] = N'hr,order,purch,calib,cash' WHERE [IsAdmin] = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Programs",
                schema: "auth",
                table: "AspNetUsers");
        }
    }
}
