using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Web.Reports.Migrations
{
    /// <inheritdoc />
    public partial class OrderMoldRegisterNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // เลขเอกสารควบคุมของ ทะเบียนแม่พิมพ์ (RPT-21, ผลผลิตและORDER #137) as the sample prints it: PC-DM001-1/2547 ฉบับที่1.
            migrationBuilder.Sql("""
                INSERT INTO report.DocumentNumbers ([Key], Name, Code, Edition, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt)
                SELECT v.[Key], v.Name, v.Code, v.Edition, 'system', SYSDATETIMEOFFSET(), 'system', SYSDATETIMEOFFSET()
                FROM (VALUES (N'RPT-21-M', N'ทะเบียนแม่พิมพ์', N'PC-DM001-1/2547', N' ฉบับที่1')) AS v([Key], Name, Code, Edition)
                WHERE NOT EXISTS (SELECT 1 FROM report.DocumentNumbers d WHERE d.[Key] = v.[Key]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM report.DocumentNumbers WHERE [Key] = N'RPT-21-M';");
        }
    }
}
