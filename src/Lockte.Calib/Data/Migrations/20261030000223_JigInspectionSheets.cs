using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Calib.Data.Migrations
{
    /// <inheritdoc />
    public partial class JigInspectionSheets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JigInspectionSheets",
                schema: "calib",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ModelKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DocumentKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DocumentNo = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JigInspectionSheets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JigInspectionPoints",
                schema: "calib",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SheetId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    Tolerance = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ToleranceValue = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JigInspectionPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JigInspectionPoints_JigInspectionSheets_SheetId",
                        column: x => x.SheetId,
                        principalSchema: "calib",
                        principalTable: "JigInspectionSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JigInspectionPoints_SheetId_SortOrder",
                schema: "calib",
                table: "JigInspectionPoints",
                columns: new[] { "SheetId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_JigInspectionSheets_DocumentKey",
                schema: "calib",
                table: "JigInspectionSheets",
                column: "DocumentKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JigInspectionSheets_Type_ModelKey",
                schema: "calib",
                table: "JigInspectionSheets",
                columns: new[] { "Type", "ModelKey" },
                unique: true);

            // The starting data of the 42 sheets (#199): the values of reports.md RPT-05/06, from the embedded Seed/jig-inspection-sheets.tsv.
            foreach (var sheet in JigInspectionSeed.Sheets)
            {
                var type = sheet.Type.ToString();
                migrationBuilder.Sql(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"""
                    INSERT INTO calib.JigInspectionSheets ([Type], ModelKey, Title, DocumentKey, DocumentNo, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt)
                    VALUES (N'{type}', N'{Quote(sheet.ModelKey)}', N'{Quote(sheet.Title)}', N'{sheet.DocumentKey}', {sheet.DocumentNo}, N'system', SYSDATETIMEOFFSET(), N'system', SYSDATETIMEOFFSET());
                    """));
                for (var i = 0; i < sheet.Points.Count; i++)
                {
                    var p = sheet.Points[i];
                    var tolerance = p.ToleranceValue is { } t ? t.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "NULL";
                    migrationBuilder.Sql(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"""
                        INSERT INTO calib.JigInspectionPoints (SheetId, SortOrder, Label, Kind, [Value], Tolerance, ToleranceValue, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt)
                        SELECT s.Id, {i + 1}, N'{p.Label}', N'{p.Kind}', {p.Value:0.00}, N'{p.Tolerance}', {tolerance}, N'system', SYSDATETIMEOFFSET(), N'system', SYSDATETIMEOFFSET()
                        FROM calib.JigInspectionSheets s WHERE s.[Type] = N'{type}' AND s.ModelKey = N'{Quote(sheet.ModelKey)}';
                        """));
                }
            }
        }

        private static string Quote(string text) => text.Replace("'", "''");
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JigInspectionPoints",
                schema: "calib");

            migrationBuilder.DropTable(
                name: "JigInspectionSheets",
                schema: "calib");
        }
    }
}
