using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Web.Reports.Migrations
{
    /// <inheritdoc />
    public partial class JigGrindingDocumentNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // เลขเอกสารควบคุมของแบบตรวจ JIG กรอ 068, 168 (WI-QA049-57, 58/2560; #478). Only a number that is not there yet is added, so it can run again.
            foreach (var d in Lockte.Calib.JigInspectionSeed.Documents.Where(d => d.Key is "JIG-G-57" or "JIG-G-58"))
                migrationBuilder.Sql($"""
                    INSERT INTO report.DocumentNumbers ([Key], Name, Code, Edition, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt)
                    SELECT N'{d.Key}', N'{d.Name}', N'{d.Code}', N'{d.Edition}', 'system', SYSDATETIMEOFFSET(), 'system', SYSDATETIMEOFFSET()
                    WHERE NOT EXISTS (SELECT 1 FROM report.DocumentNumbers x WHERE x.[Key] = N'{d.Key}');
                    """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
