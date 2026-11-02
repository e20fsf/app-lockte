using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Calib.Data.Migrations
{
    /// <inheritdoc />
    public partial class JigImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ImageId",
                schema: "calib",
                table: "JigInspectionSheets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JigImages",
                schema: "calib",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Data = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JigImages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JigInspectionSheets_ImageId",
                schema: "calib",
                table: "JigInspectionSheets",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_JigImages_Sha256",
                schema: "calib",
                table: "JigImages",
                column: "Sha256",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_JigInspectionSheets_JigImages_ImageId",
                schema: "calib",
                table: "JigInspectionSheets",
                column: "ImageId",
                principalSchema: "calib",
                principalTable: "JigImages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JigInspectionSheets_JigImages_ImageId",
                schema: "calib",
                table: "JigInspectionSheets");

            migrationBuilder.DropTable(
                name: "JigImages",
                schema: "calib");

            migrationBuilder.DropIndex(
                name: "IX_JigInspectionSheets_ImageId",
                schema: "calib",
                table: "JigInspectionSheets");

            migrationBuilder.DropColumn(
                name: "ImageId",
                schema: "calib",
                table: "JigInspectionSheets");
        }
    }
}
