using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Web.Reports.Migrations
{
    /// <inheritdoc />
    public partial class JigDocumentNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // เลขเอกสารควบคุมของแบบตรวจ JIG 42 ใบ (WI-QA049-1…23/2550 วัดศูนย์, 24…56/2560 กรอ; ฉบับที่ 2) — สอบเทียบ #199.
            foreach (var d in Lockte.Calib.JigInspectionSeed.Documents)
                migrationBuilder.Sql($"""
                    INSERT INTO report.DocumentNumbers ([Key], Name, Code, Edition, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt)
                    SELECT N'{d.Key}', N'{d.Name.Replace("'", "''")}', N'{d.Code}', N'{d.Edition}', 'system', SYSDATETIMEOFFSET(), 'system', SYSDATETIMEOFFSET()
                    WHERE NOT EXISTS (SELECT 1 FROM report.DocumentNumbers x WHERE x.[Key] = N'{d.Key}');
                    """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM report.DocumentNumbers WHERE [Key] LIKE N'JIG-[ZG]-%';");
        }
    }
}
