using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Web.Reports.Migrations
{
    /// <inheritdoc />
    public partial class DeliveryDocumentNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // เลขเอกสารควบคุมของเอกสาร ISO การส่งของ (ผลผลิตและORDER #136) as the samples print them (print-samples.md).
            migrationBuilder.Sql("""
                INSERT INTO report.DocumentNumbers ([Key], Name, Code, Edition, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt)
                SELECT v.[Key], v.Name, v.Code, v.Edition, 'system', SYSDATETIMEOFFSET(), 'system', SYSDATETIMEOFFSET()
                FROM (VALUES
                    (N'RPT-05', N'DELIVERY SCHEDULE', N'PC-PD003-3/2547', N''),
                    (N'RPT-07', N'ใบรายงานการส่งสินค้า', N'WI-ST005-1/2551', N' ฉบับที่2'),
                    (N'RPT-07-Q', N'รายงานการตรวจสอบสินค้าที่จัดส่งให้ลูกค้า', N'WI-QA033-1/2547', N' ฉบับที่ 3'),
                    (N'RPT-08-M', N'รายงานส่งผู้จัดการโรงงาน', N'WI-ST005-3/2551', N' ฉบับที่ 1')
                ) AS v([Key], Name, Code, Edition)
                WHERE NOT EXISTS (SELECT 1 FROM report.DocumentNumbers d WHERE d.[Key] = v.[Key]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM report.DocumentNumbers WHERE [Key] IN (N'RPT-05', N'RPT-07', N'RPT-07-Q', N'RPT-08-M');");
        }
    }
}
