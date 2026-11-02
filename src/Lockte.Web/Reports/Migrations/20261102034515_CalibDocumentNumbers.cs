using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Web.Reports.Migrations
{
    /// <inheritdoc />
    public partial class CalibDocumentNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // เลขเอกสารควบคุมของเอกสารสอบเทียบ: แผนการสอบเทียบ, บัญชีรายชื่อเครื่องมือวัด ภายใน/ภายนอก, บันทึกผลการสอบเทียบ — สอบเทียบ #202.
            migrationBuilder.Sql("""
                INSERT INTO report.DocumentNumbers ([Key], Name, Code, Edition, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt)
                SELECT v.[Key], v.Name, v.Code, v.Edition, 'system', SYSDATETIMEOFFSET(), 'system', SYSDATETIMEOFFSET()
                FROM (VALUES (N'RPT-01-C', N'แผนการสอบเทียบ', N'PC-QA009-2/2547', N' ฉบับที่ 2'),
                             (N'RPT-02-C-I', N'บัญชีรายชื่อเครื่องมือวัด (สอบเทียบภายใน)', N'PC-QA009-1/2552', N' ฉบับที่ 2'),
                             (N'RPT-02-C-E', N'บัญชีรายชื่อเครื่องมือวัด (สอบเทียบภายนอก)', N'PC-QA009-1/2547', N' ฉบับที่ 2'),
                             (N'RPT-03-C', N'บันทึกผลการสอบเทียบ', N'PC-QA009-4/2547', N' ฉบับที่ 2')) AS v([Key], Name, Code, Edition)
                WHERE NOT EXISTS (SELECT 1 FROM report.DocumentNumbers d WHERE d.[Key] = v.[Key]);
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM report.DocumentNumbers WHERE [Key] IN (N'RPT-01-C', N'RPT-02-C-I', N'RPT-02-C-E', N'RPT-03-C');");
        }
    }
}
