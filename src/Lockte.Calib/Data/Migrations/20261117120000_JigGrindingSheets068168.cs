using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Calib.Data.Migrations
{
    /// <inheritdoc />
    public partial class JigGrindingSheets068168 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // แบบตรวจ JIG กรอ ของ 068 และ 168 (#478): added from the starting data, only when the model has no grinding sheet yet — a sheet QA already made
            // (or edited) is never touched, so the step can run again.
            foreach (var sheet in JigInspectionSeed.Sheets.Where(s => s.Type == JigSheetType.Grinding && s.ModelKey is "068" or "168"))
            {
                var type = sheet.Type.ToString();
                migrationBuilder.Sql(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"""
                    IF NOT EXISTS (SELECT 1 FROM calib.JigInspectionSheets WHERE [Type] = N'{type}' AND ModelKey = N'{sheet.ModelKey}')
                       AND NOT EXISTS (SELECT 1 FROM calib.JigInspectionSheets WHERE DocumentKey = N'{sheet.DocumentKey}')
                        INSERT INTO calib.JigInspectionSheets ([Type], ModelKey, Title, DocumentKey, DocumentNo, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt)
                        VALUES (N'{type}', N'{sheet.ModelKey}', N'{sheet.Title}', N'{sheet.DocumentKey}', {sheet.DocumentNo}, N'system', SYSDATETIMEOFFSET(), N'system', SYSDATETIMEOFFSET());
                    """));
                for (var i = 0; i < sheet.Points.Count; i++)
                {
                    var p = sheet.Points[i];
                    migrationBuilder.Sql(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"""
                        INSERT INTO calib.JigInspectionPoints (SheetId, SortOrder, Label, Kind, [Value], Tolerance, ToleranceValue, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt)
                        SELECT s.Id, {i + 1}, N'{p.Label}', N'{p.Kind}', {p.Value:0.00}, N'{p.Tolerance}', {p.ToleranceValue:0.00}, N'system', SYSDATETIMEOFFSET(), N'system', SYSDATETIMEOFFSET()
                        FROM calib.JigInspectionSheets s WHERE s.[Type] = N'{type}' AND s.ModelKey = N'{sheet.ModelKey}'
                          AND NOT EXISTS (SELECT 1 FROM calib.JigInspectionPoints x WHERE x.SheetId = s.Id);
                        """));
                }
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
