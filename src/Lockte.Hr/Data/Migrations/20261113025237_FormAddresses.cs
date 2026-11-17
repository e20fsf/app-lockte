using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Hr.Data.Migrations
{
    /// <inheritdoc />
    public partial class FormAddresses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OfficeDistrict",
                schema: "hr",
                table: "FormSigners",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficeHouseNo",
                schema: "hr",
                table: "FormSigners",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficeMoo",
                schema: "hr",
                table: "FormSigners",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficePhone",
                schema: "hr",
                table: "FormSigners",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficeProvince",
                schema: "hr",
                table: "FormSigners",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficeRoad",
                schema: "hr",
                table: "FormSigners",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficeSoi",
                schema: "hr",
                table: "FormSigners",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficeSubdistrict",
                schema: "hr",
                table: "FormSigners",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkplaceDistrict",
                schema: "hr",
                table: "FormSigners",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkplaceHouseNo",
                schema: "hr",
                table: "FormSigners",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkplaceMoo",
                schema: "hr",
                table: "FormSigners",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkplacePhone",
                schema: "hr",
                table: "FormSigners",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkplaceProvince",
                schema: "hr",
                table: "FormSigners",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkplaceRoad",
                schema: "hr",
                table: "FormSigners",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkplaceSoi",
                schema: "hr",
                table: "FormSigners",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkplaceSubdistrict",
                schema: "hr",
                table: "FormSigners",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OfficeDistrict",
                schema: "hr",
                table: "FormSigners");

            migrationBuilder.DropColumn(
                name: "OfficeHouseNo",
                schema: "hr",
                table: "FormSigners");

            migrationBuilder.DropColumn(
                name: "OfficeMoo",
                schema: "hr",
                table: "FormSigners");

            migrationBuilder.DropColumn(
                name: "OfficePhone",
                schema: "hr",
                table: "FormSigners");

            migrationBuilder.DropColumn(
                name: "OfficeProvince",
                schema: "hr",
                table: "FormSigners");

            migrationBuilder.DropColumn(
                name: "OfficeRoad",
                schema: "hr",
                table: "FormSigners");

            migrationBuilder.DropColumn(
                name: "OfficeSoi",
                schema: "hr",
                table: "FormSigners");

            migrationBuilder.DropColumn(
                name: "OfficeSubdistrict",
                schema: "hr",
                table: "FormSigners");

            migrationBuilder.DropColumn(
                name: "WorkplaceDistrict",
                schema: "hr",
                table: "FormSigners");

            migrationBuilder.DropColumn(
                name: "WorkplaceHouseNo",
                schema: "hr",
                table: "FormSigners");

            migrationBuilder.DropColumn(
                name: "WorkplaceMoo",
                schema: "hr",
                table: "FormSigners");

            migrationBuilder.DropColumn(
                name: "WorkplacePhone",
                schema: "hr",
                table: "FormSigners");

            migrationBuilder.DropColumn(
                name: "WorkplaceProvince",
                schema: "hr",
                table: "FormSigners");

            migrationBuilder.DropColumn(
                name: "WorkplaceRoad",
                schema: "hr",
                table: "FormSigners");

            migrationBuilder.DropColumn(
                name: "WorkplaceSoi",
                schema: "hr",
                table: "FormSigners");

            migrationBuilder.DropColumn(
                name: "WorkplaceSubdistrict",
                schema: "hr",
                table: "FormSigners");
        }
    }
}
