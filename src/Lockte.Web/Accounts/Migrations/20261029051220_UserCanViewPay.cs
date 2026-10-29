using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Web.Accounts.Migrations
{
    /// <inheritdoc />
    public partial class UserCanViewPay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // #277: every user that exists now keeps seeing ข้อมูลค่าจ้าง (the admin account ignores the flag);
            // users added afterwards get the value the admin chose, written by the app.
            migrationBuilder.AddColumn<bool>(
                name: "CanViewPay",
                schema: "auth",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanViewPay",
                schema: "auth",
                table: "AspNetUsers");
        }
    }
}
