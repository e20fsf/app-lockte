using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Hr.Data.Migrations
{
    /// <inheritdoc />
    public partial class OvertimeSheetEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OvertimeSheetEntries",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkTimeId = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    Memo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OvertimeSheetEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OvertimeSheetEntries_WorkTimes_WorkTimeId",
                        column: x => x.WorkTimeId,
                        principalSchema: "hr",
                        principalTable: "WorkTimes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeSheetEntries_DepartmentId",
                schema: "hr",
                table: "OvertimeSheetEntries",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeSheetEntries_WorkTimeId",
                schema: "hr",
                table: "OvertimeSheetEntries",
                column: "WorkTimeId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OvertimeSheetEntries",
                schema: "hr");
        }
    }
}
